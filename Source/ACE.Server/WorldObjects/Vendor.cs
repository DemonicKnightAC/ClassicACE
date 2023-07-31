using System;
using System.Collections.Generic;
using System.Linq;

using log4net;

using ACE.Common;
using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Managers;
using ACE.Server.Factories.Enum;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects.Entity;

namespace ACE.Server.WorldObjects
{
    public class VendorItemComparer : IComparer<WorldObject>
    {
        public VendorItemComparer()
        {
        }

        public int Compare(WorldObject a, WorldObject b)
        {
            var result = a.WeenieType.CompareTo(b.WeenieType);

            if (result == 0)
            {
                result = a.ItemType.CompareTo(b.ItemType);
                if (result == 0)
                {
                    if (a.WeaponSkill != Skill.None)
                        result = a.WeaponSkill.CompareTo(b.WeaponSkill);
                    else if (a.ArmorType != 0)
                    {
                        int armorTypeA = a.ArmorType ?? 0;
                        int armorTypeB = b.ArmorType ?? 0;
                        result = armorTypeA.CompareTo(armorTypeB);
                    }

                    if (result == 0)
                    {
                        result = a.WeenieClassId.CompareTo(b.WeenieClassId);
                        if (result == 0)
                        {
                            result = a.Name.CompareTo(b.Name);
                        }
                    }
                }
            }

            return result;
        }
    }

    /// <summary>
    /// ** Buy Data Flow **
    ///
    /// Player.HandleActionBuyItem -> Vendor.BuyItems_ValidateTransaction -> Player.FinalizeBuyTransaction -> Vendor.BuyItems_FinalTransaction
    ///
    /// </summary>
    public class Vendor : Creature
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly VendorItemComparer VendorItemComparer = new VendorItemComparer();

        public readonly Dictionary<ObjectGuid, WorldObject> DefaultItemsForSale = new Dictionary<ObjectGuid, WorldObject>();

        // unique items purchased from other players
        public Dictionary<ObjectGuid, WorldObject> UniqueItemsForSale = new Dictionary<ObjectGuid, WorldObject>();

        private bool inventoryloaded { get; set; }

        /// <summary>
        ///  The last player who used this vendor
        /// </summary>
        private WorldObjectInfo lastPlayerInfo { get; set; }

        private DateTime LastRestockTime;

        public uint? AlternateCurrency
        {
            get => GetProperty(PropertyDataId.AlternateCurrency);
            set { if (!value.HasValue) RemoveProperty(PropertyDataId.AlternateCurrency); else SetProperty(PropertyDataId.AlternateCurrency, value.Value); }
        }

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Vendor(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            Biota.PartialPersitanceFilter = new List<PartialPersistanceEntry>()
            {
                { new PartialPersistanceEntry(PropertyInt.MoneyIncome)  },
                { new PartialPersistanceEntry(PropertyInt.MoneyOutflow) },
                { new PartialPersistanceEntry(PropertyInt.RecentMoneyIncome) },
                { new PartialPersistanceEntry(PropertyInt.RecentMoneyOutflow) },
                { new PartialPersistanceEntry(PropertyInt.NumItemsSold) },
                { new PartialPersistanceEntry(PropertyInt.NumItemsBought) },
                { new PartialPersistanceEntry(PropertyInt.NumServicesSold) }
            };

            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Vendor(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            ObjectDescriptionFlags |= ObjectDescriptionFlag.Vendor;

            if (!PropertyManager.GetBool("vendor_shop_uses_generator").Item)
            {
                GeneratorProfiles.RemoveAll(p => p.Biota.WhereCreate.HasFlag(RegenLocationType.Shop));
            }

            LastRestockTime = DateTime.UnixEpoch;
            OpenForBusiness = ValidateVendorRequirements();
        }

        private bool ValidateVendorRequirements()
        {
            var success = true;

            var currencyWCID = AlternateCurrency ?? (uint)ACE.Entity.Enum.WeenieClassName.W_COINSTACK_CLASS;
            var currencyWeenie = DatabaseManager.World.GetCachedWeenie(currencyWCID);
            if (currencyWeenie == null)
            {
                var errorMsg = $"WCID {currencyWCID}{(AlternateCurrency.HasValue ? ", which comes from PropertyDataId.AlternateCurrency," : "")} is not found in the database, Vendor has been disabled as a result!";
                log.Error($"[VENDOR] {Name} (0x{Guid}:{WeenieClassId}) Currency {errorMsg}");
                success = false;
            }

            if (!MerchandiseItemTypes.HasValue)
            {
                log.Error($"[VENDOR] {Name} (0x{Guid}:{WeenieClassId}) MerchandiseItemTypes is NULL, Vendor has been disabled as a result!");
                success = false;
            }

            if (!MerchandiseMinValue.HasValue)
            {
                log.Error($"[VENDOR] {Name} (0x{Guid}:{WeenieClassId}) MerchandiseMinValue is NULL, Vendor has been disabled as a result!");
                success = false;
            }

            if (!MerchandiseMaxValue.HasValue)
            {
                log.Error($"[VENDOR] {Name} (0x{Guid}:{WeenieClassId}) MerchandiseMaxValue is NULL, Vendor has been disabled as a result!");
                success = false;
            }

            if (!BuyPrice.HasValue)
            {
                log.Error($"[VENDOR] {Name} (0x{Guid}:{WeenieClassId}) BuyPrice is NULL, Vendor has been disabled as a result!");
                success = false;
            }

            if (!SellPrice.HasValue)
            {
                log.Error($"[VENDOR] {Name} (0x{Guid}:{WeenieClassId}) SellPrice is NULL, Vendor has been disabled as a result!");
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Populates this vendor's DefaultItemsForSale
        /// </summary>
        private void LoadInventory()
        {
            if (inventoryloaded) return;

            var itemsForSale = new Dictionary<(uint weenieClassId, int paletteTemplate, double shade), uint>();

            foreach (var item in Biota.PropertiesCreateList.Where(x => x.DestinationType == DestinationType.Shop))
                LoadInventoryItem(itemsForSale, item.WeenieClassId, item.Palette, item.Shade, item.StackSize);

            //if (Biota.PropertiesGenerator != null && !PropertyManager.GetBool("vendor_shop_uses_generator").Item)
            //{
            //    foreach (var item in Biota.PropertiesGenerator.Where(x => x.WhereCreate.HasFlag(RegenLocationType.Shop)))
            //        LoadInventoryItem(itemsForSale, item.WeenieClassId, (int?)item.PaletteId, item.Shade, item.StackSize);
            //}

            inventoryloaded = true;
        }

        private void LoadInventoryItem(Dictionary<(uint weenieClassId, int paletteTemplate, double shade), uint> itemsForSale,
            uint weenieClassId, int? palette, float? shade, int? stackSize)
        {
            //var itemProfile = (weenieClassId, palette ?? 0, shade ?? 0);

            // let's skip dupes if there are any
            //if (itemsForSale.ContainsKey(itemProfile))
            //    return;

            var wo = WorldObjectFactory.CreateNewWorldObject(weenieClassId);

            if (wo == null) return;

            if (palette > 0)
                wo.PaletteTemplate = palette;

            if (shade > 0)
                wo.Shade = shade;

            wo.ContainerId = Guid.Full;

            wo.CalculateObjDesc();

            //itemsForSale.Add(itemProfile, wo.Guid.Full);

            wo.VendorShopCreateListStackSize = stackSize ?? -1;

            DefaultItemsForSale.Add(wo.Guid, wo);
        }

        public void AddDefaultItem(WorldObject item)
        {
            var existing = GetDefaultItemsByWcid(item.WeenieClassId);

            // add to existing stack?
            if (existing.Count > 0)
            {
                // vendors do not respect MaxStackSize, this fixes items such as mortar and pestles having a lot of entries.
                var stack = existing.First();
                stack.MaxStackSize = (ushort)((stack.StackSize ?? 1) + (item.StackSize ?? 1));
                stack.SetStackSize((stack.StackSize ?? 1) + (item.StackSize ?? 1));
                return;

                //var stackLeft = existing.FirstOrDefault(i => (i.StackSize ?? 1) < (i.MaxStackSize ?? 1));
                //if (stackLeft != null)
                //{
                //    stackLeft.SetStackSize((stackLeft.StackSize ?? 1) + 1);
                //    return;
                //}
            }

            // create new item
            item.ContainerId = Guid.Full;

            item.CalculateObjDesc();

            DefaultItemsForSale.Add(item.Guid, item);
        }

        /// <summary>
        /// Helper function to replace the previous 'AllItemsForSale' combiner
        /// While AllItemsForSale was a useful concept, it was only used in 2 places, and was inefficient
        /// </summary>
        public void forEachItem(Action<WorldObject> action)
        {
            foreach (var kvp in DefaultItemsForSale)
                action(kvp.Value);

            foreach (var kvp in UniqueItemsForSale)
                action(kvp.Value);
        }

        public List<WorldObject> GetDefaultItemsByWcid(uint wcid)
        {
            return DefaultItemsForSale.Values.Where(i => i.WeenieClassId == wcid).ToList();
        }

        /// <summary>
        /// Searches the vendor's inventory for an item
        /// </summary>
        public bool TryGetItemForSale(ObjectGuid itemGuid, out WorldObject itemForSale)
        {
            return DefaultItemsForSale.TryGetValue(itemGuid, out itemForSale) || UniqueItemsForSale.TryGetValue(itemGuid, out itemForSale);
        }

        /// <summary>
        /// This is raised by Player.HandleActionUseItem.<para />
        /// The item does not exist in the players possession.<para />
        /// If the item was outside of range, the player will have been commanded to move using DoMoveTo before ActOnUse is called.<para />
        /// When this is called, it should be assumed that the player is within range.
        /// </summary>
        public override void ActOnUse(WorldObject wo)
        {
            var player = wo as Player;
            if (player == null) return;

            OpenVendor(player);
        }

        private void OpenVendor(Player player, string filter = null)
        {
            if (player.IsBusy)
            {
                player.SendWeenieError(WeenieError.YoureTooBusy);
                return;
            }

            if (!OpenForBusiness || !ValidateVendorRequirements())
            {
                // should there be some sort of feedback to player here?
                return;
            }

            var rotateTime = Rotate(player);    // vendor rotates towards player

            // TODO: remove this when DelayManager is not forward propagating current tick time

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(0.001f);  // force to run after rotate.EnqueueBroadcastAction
            actionChain.AddAction(this, LoadInventory);
            actionChain.AddDelaySeconds(rotateTime);
            actionChain.AddAction(this, () => ApproachVendor(player, VendorType.Open, 0, filter));
            actionChain.EnqueueChain();

            if (lastPlayerInfo == null)
            {
                var closeChain = new ActionChain();
                closeChain.AddDelaySeconds(closeInterval);
                closeChain.AddAction(this, CheckClose);
                closeChain.EnqueueChain();
            }

            lastPlayerInfo = new WorldObjectInfo(player);
        }

        private double BuyPriceMod;
        private double SellPriceMod;

        public void AddMoneyIncome(int value)
        {
            MoneyIncome += value;
            RecentMoneyIncome += value;
        }

        public void AddMoneyOutflow(int value)
        {
            MoneyOutflow += value;
            RecentMoneyOutflow += value;
        }

        public void UpdateHappyVendor()
        {
            BuyPriceMod = 0.0f;
            SellPriceMod = 0.0f;

            if (WeenieClassId == (uint)Factories.Enum.WeenieClassName.customdm_consignmentvendor)
                return;

            int vendorHappyMean = (VendorHappyMean ?? 0) * Math.Min(MerchandiseMaxValue ?? 3000, 50000) / 3;
            int vendorHappyVariance = (VendorHappyVariance ?? 0) * Math.Min(MerchandiseMaxValue ?? 3000, 50000) / 3;
            if (vendorHappyMean == 0)
                return;

            int currentVendorHappyThreshold = ThreadSafeRandom.Next(vendorHappyMean, vendorHappyMean + vendorHappyVariance);
            double priceMod = Math.Clamp((RecentMoneyIncome + RecentMoneyOutflow) / (float)currentVendorHappyThreshold * 0.1f, 0.0f, 0.1f);

            SellPriceMod = priceMod;
            BuyPriceMod = 0.1f - priceMod;
        }

        /// <summary>
        /// Sends the latest vendor inventory list to player, rotates vendor towards player, and performs the appropriate emote.
        /// </summary>
        /// <param name="action">The action performed by the player</param>
        public void ApproachVendor(Player player, VendorType action = VendorType.Undef, uint altCurrencySpent = 0, string filter = null)
        {
            if(VendorPKOnly && player.PlayerKillerStatus != PlayerKillerStatus.PK)
            {
                player.Session.Network.EnqueueSend(new GameEventTell(this, "Come back when you're not under Asheron's protection or Bael'Zharon's respite.", player, ChatMessageType.Tell));
                return;
            }

            player.LastVendorFilter = filter;

            if (WeenieClassId == (uint)Factories.Enum.WeenieClassName.customdm_consignmentvendor)
            {
                LoadConsignments(player, filter);
                return;
            }

            RotUniques();

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                RestockRandomItems();

            player.Session.Network.EnqueueSend(new GameEventApproachVendor(player.Session, this, altCurrencySpent));

            var rotateTime = Rotate(player); // vendor rotates to player

            if (action != VendorType.Undef)
                DoVendorEmote(action, player);

            player.LastOpenedContainerId = Guid;
        }

        private void LoadConsignments(Player player, string filter)
        {
            UniqueItemsForSale.Clear();
            switch (filter)
            {
                case "manage":
                    LoadConsignmentsForSeller(player);
                    break;
                case "relist":
                    ReListConsignments(player);
                    break;
                default:
                    LoadConsignmentsForBuyer(player, filter);
                    break;
            }
        }

        private void LoadConsignmentsForSeller(Player player)
        {
            DatabaseManager.Shard.GetConsignmentsBySellerId(player.Guid.Full, consignments =>
            {
                LoadConsignments(player, consignments);
            });

            DatabaseManager.Shard.GetConsignmentCompletesBySellerId(player.Guid.Full, consignmentCompletes =>
            {
                LoadConsignmentCompletes(player, consignmentCompletes);
            });
        }

        private void LoadConsignmentsForBuyer(Player player, string filter)
        {
            DatabaseManager.Shard.GetConsignmentsByFilter(filter, consignments =>
            {
                LoadConsignments(player, consignments);
            });
        }

        private void LoadConsignments(Player player, List<Consignment> consignments)
        {
            foreach (var consignment in consignments)
            {
                var item =  WorldObjectFactory.CreateWorldObject(consignment.Item);
                var desc = item.GetProperty(PropertyString.LongDesc);
                item.Container = WorldObjectFactory.CreateWorldObject(consignment.Box) as Container;

                //Overriding the value is a bit of a hack due ot client limitations. It is undone when the item is sold.
                if (player.Guid == consignment.SellerId)
                    item.SetProperty(PropertyInt.Value, 0);
                else
                    item.SetProperty(PropertyInt.Value, (int)consignment.Price);
                item.SetProperty(PropertyString.LongDesc, $"{desc}\nOriginal Value: {consignment.OriginalValue}");

                UniqueItemsForSale.Add(item.Guid, item);
            }

            player.Session.Network.EnqueueSend(new GameEventApproachVendor(player.Session, this, 0));

            Rotate(player);

            player.LastOpenedContainerId = Guid;
        }

        private void LoadConsignmentCompletes(Player player, List<ConsignmentComplete> consignmentCompletes)
        {
            foreach (var consignmentComplete in consignmentCompletes)
            {
                var item =  WorldObjectFactory.CreateWorldObject(consignmentComplete.Item);
                item.Container = WorldObjectFactory.CreateWorldObject(consignmentComplete.Box) as Container;

                if (consignmentComplete.SoldTime > 0)
                {
                    AddPayoutSack(consignmentComplete, item);
                }
                else
                {
                    item.IconOverlayId = 0x060011F8;
                    UniqueItemsForSale.Add(item.Guid, item);
                }
            }

            player.Session.Network.EnqueueSend(new GameEventApproachVendor(player.Session, this, 0));

            Rotate(player);

            player.LastOpenedContainerId = Guid;
        }

        public void AddPayoutSack(ConsignmentComplete consignmentComplete, WorldObject item)
        {
            var payout = (uint)Math.Round(consignmentComplete.Price * 0.9);
            var paymentSack = (Container)WorldObjectFactory
                .CreateNewWorldObject((uint)Factories.Enum.WeenieClassName.customdm_consignmentpaymentsack);
            paymentSack.ConsignmentComplete = consignmentComplete;
            paymentSack.ContainerId = Guid.Full;
            paymentSack.Container = this;
            paymentSack.LongDesc = $"This sack contains {payout} pyreals in payment for the sale of {item.Name}.";

            UniqueItemsForSale.Add(paymentSack.Guid, paymentSack);
        }

        public void ReListConsignments(Player player)
        {
            //Calling this just to handle any pending expiration migrations
            DatabaseManager.Shard.GetConsignmentsBySellerId(player.Guid.Full, consignment => { });

            DatabaseManager.Shard.GetConsignmentCompletesBySellerId(player.Guid.Full, consignmentCompletes =>
            {
                foreach (var consignmentComplete in consignmentCompletes)
                {
                    if (consignmentComplete.SoldTime > 0)
                        return;

                    DatabaseManager.Shard.ReListConsignment(consignmentComplete, result => {});
                }
            });
        }

        public void DoVendorEmote(VendorType vendorType, WorldObject player)
        {
            switch (vendorType)
            {
                case VendorType.Open:
                    EmoteManager.DoVendorEmote(vendorType, player);
                    break;

                case VendorType.Buy:    // player buys item from vendor
                    EmoteManager.DoVendorEmote(vendorType, player);
                    if (DefaultItemsForSale.Count == 0 && UniqueItemsForSale.Count == 0)
                        EmoteManager.ExecuteEmoteSet(EmoteManager.GetEmoteSet(EmoteCategory.Vendor, null, VendorType.SoldOut), player, true);
                    break;

                case VendorType.Sell:   // player sells item to vendor
                    EmoteManager.DoVendorEmote(vendorType, player);
                    break;

                default:
                    log.Warn($"Vendor.DoVendorEmote - Encountered Unhandled VendorType {vendorType} for {Name} ({WeenieClassId})");
                    break;
            }
        }

        private static readonly float closeInterval = 1.5f;

        /// <summary>
        /// After a player approaches a vendor, this is called every closeInterval seconds
        /// to see if the player is still within the UseRadius of the vendor.
        ///
        /// If the player has moved away, the vendor Close emote is called (waving goodbye, saying farewell)
        /// </summary>
        public void CheckClose()
        {
            if (lastPlayerInfo == null)
                return;

            var lastPlayer = lastPlayerInfo.TryGetWorldObject() as Player;

            if (lastPlayer == null)
            {
                lastPlayerInfo = null;
                return;
            }

            // handles player logging out at vendor
            if (lastPlayer.CurrentLandblock == null)
            {
                lastPlayerInfo = null;
                return;
            }

            var dist = GetCylinderDistance(lastPlayer);

            if (dist > UseRadius)
            {
                if (lastPlayer.LastOpenedContainerId == Guid)
                    lastPlayer.LastOpenedContainerId = ObjectGuid.Invalid;

                EmoteManager.DoVendorEmote(VendorType.Close, lastPlayer);
                lastPlayerInfo = null;

                return;
            }

            var closeChain = new ActionChain();
            closeChain.AddDelaySeconds(closeInterval);
            closeChain.AddAction(this, CheckClose);
            closeChain.EnqueueChain();
        }

        /// <summary>
        /// Creates world objects for generic items
        /// </summary>
        private List<WorldObject> ItemProfileToWorldObjects(ItemProfile itemProfile)
        {
            var results = new List<WorldObject>();

            var remaining = itemProfile.Amount;

            while (remaining > 0)
            {
                var wo = WorldObjectFactory.CreateNewWorldObject(itemProfile.WeenieClassId);

                if (itemProfile.Palette != null)
                    wo.PaletteTemplate = itemProfile.Palette;

                if (itemProfile.Shade != null)
                    wo.Shade = itemProfile.Shade;

                if ((wo.MaxStackSize ?? 0) > 0)
                {
                    // stackable
                    var currentStackSize = Math.Min(remaining, wo.MaxStackSize.Value);

                    wo.SetStackSize(currentStackSize);
                    results.Add(wo);
                    remaining -= currentStackSize;
                }
                else
                {
                    // non-stackable
                    wo.StackSize = null;
                    results.Add(wo);
                    remaining--;
                }
            }
            return results;
        }

        /// <summary>
        /// Handles validation for player buying items from vendor
        /// </summary>
        public bool BuyItems_ValidateTransaction(List<ItemProfile> itemProfiles, Player player)
        {
            // one difference between buy and sell currently
            // is that if *any* items in the buy transactions are detected as invalid,
            // we reject the entire transaction.
            // this seems to be the "safest" route, however in terms of player convenience
            // where only 1 item has an error from a large purchase set,
            // this might not be the most convenient for the player.

            var defaultItemProfiles = new List<ItemProfile>();
            var uniqueItems = new List<WorldObject>();

            // find item profiles in default and unique items
            foreach (var itemProfile in itemProfiles)
            {
                if (!itemProfile.IsValidAmount)
                {
                    // reject entire transaction immediately
                    player.SendTransientError($"Invalid amount");
                    return false;
                }

                var itemGuid = new ObjectGuid(itemProfile.ObjectGuid);

                // check default items
                if (DefaultItemsForSale.TryGetValue(itemGuid, out var defaultItemForSale))
                {
                    itemProfile.WeenieClassId = defaultItemForSale.WeenieClassId;
                    itemProfile.Palette = defaultItemForSale.PaletteTemplate;
                    itemProfile.Shade = defaultItemForSale.Shade;

                    defaultItemProfiles.Add(itemProfile);
                }
                // check unique items
                else if (UniqueItemsForSale.TryGetValue(itemGuid, out var uniqueItemForSale))
                {
                    uniqueItems.Add(uniqueItemForSale);
                }
            }

            // ensure player has enough free inventory slots / container slots / available burden to receive items
            var itemsToReceive = new ItemsToReceive(player);

            foreach (var defaultItemProfile in defaultItemProfiles)
            {
                itemsToReceive.Add(defaultItemProfile.WeenieClassId, defaultItemProfile.Amount);

                if (itemsToReceive.PlayerExceedsLimits)
                    break;
            }

            if (!itemsToReceive.PlayerExceedsLimits)
            {
                foreach (var uniqueItem in uniqueItems)
                {
                    itemsToReceive.Add(uniqueItem.WeenieClassId, uniqueItem.StackSize ?? 1);

                    if (itemsToReceive.PlayerExceedsLimits)
                        break;
                }
            }

            if (itemsToReceive.PlayerExceedsLimits)
            {
                if (itemsToReceive.PlayerExceedsAvailableBurden)
                    player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, "You are too encumbered to buy that!"));
                else if (itemsToReceive.PlayerOutOfInventorySlots)
                    player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, "You do not have enough pack space to buy that!"));
                else if (itemsToReceive.PlayerOutOfContainerSlots)
                    player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, "You do not have enough container slots to buy that!"));

                return false;
            }

            // ideally the creation of the wo's would be delayed even further,
            // and all validations would be performed on weenies beforehand
            // this would require:
            // - a forEach helper function to iterate through both defaultItemProfiles (ItemProfiles) and uniqueItems (WorldObjects),
            //   so that 2 foreach iterators don't have to be written each time
            // - weenie to have more functions that mimic the functionality of WorldObject

            // create world objects for default items
            var defaultItems = new List<WorldObject>();

            foreach (var defaultItemProfile in defaultItemProfiles)
                defaultItems.AddRange(ItemProfileToWorldObjects(defaultItemProfile));

            var purchaseItems = defaultItems.Concat(uniqueItems).ToList();

            if (IsBusy && purchaseItems.Any(i => i.GetProperty(PropertyBool.VendorService) == true))
            {
                player.SendWeenieErrorWithString(WeenieErrorWithString._IsTooBusyToAcceptGifts, Name);
                CleanupCreatedItems(defaultItems);
                return false;
            }

            // check uniques
            if (!player.CheckUniques(purchaseItems, this))
            {
                CleanupCreatedItems(defaultItems);
                return false;
            }

            // calculate price
            uint totalPrice = 0;
            uint totalPriceAfterHaggling = 0;

            foreach (var item in purchaseItems)
            {
                var cost = GetSellCost(item);

                // detect rollover?
                totalPrice += cost;
                totalPriceAfterHaggling += GetHaggledCost(player, item, cost);
            }

            if(totalPriceAfterHaggling != totalPrice)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your appraisal skill allows you to haggle for a discount of {totalPrice - totalPriceAfterHaggling:N0} Pyreals!", ChatMessageType.Broadcast));
                totalPrice = totalPriceAfterHaggling;
            }

            if (ValidatePlayerHasBalance(player, totalPrice) == false)
            {
                CleanupCreatedItems(defaultItems);
                return false;
            }

            // everything is verified at this point

            // send transaction to player for further processing
            player.FinalizeBuyTransaction(this, defaultItems, uniqueItems, totalPrice);

            return true;
        }

        private bool ValidatePlayerHasBalance(Player player, uint totalPrice)
        {
            if (WeenieClassId == (uint)Factories.Enum.WeenieClassName.customdm_consignmentvendor)
            {
                if (player.CoinValue >= totalPrice)
                    return true;

                var notesNeeded = (uint)Math.Floor((double)totalPrice / 250000);
                var remainder = totalPrice % 250000;
                var notesOwned = player.GetNumInventoryItemsOfWCID((uint)Factories.Enum.WeenieClassName.tradenote250000);
                if (notesOwned >= notesNeeded && player.CoinValue > remainder)
                    return true;
            }

            if (AlternateCurrency == null)
            {
                if (player.CoinValue >= totalPrice)
                    return true;
            }
            else
            {
                var playerAltCurrency = player.GetNumInventoryItemsOfWCID(AlternateCurrency.Value);
                if (playerAltCurrency >= totalPrice)
                    return true;
            }

            return false;
        }

        private uint GetHaggledCost(Player player, WorldObject item, uint cost)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return cost;

            CreatureSkill appraisalSkill = player.GetCreatureSkill(Skill.Appraise);
            if (appraisalSkill.AdvancementClass < SkillAdvancementClass.Trained)
                return cost;

            if (item.ItemType == ItemType.PromissoryNote)
                return cost;

            if (item.Container != null && item.Container.WeenieClassId == (uint)Factories.Enum.WeenieClassName.customdm_consignmentbox)
                return cost;

            var unitCost = cost / item.StackSize ?? 1;
            var diff1 = 50 + (uint)Math.Sqrt(Math.Min(unitCost, 250000));
            var diff2 = diff1 + 50;
            var diff3 = diff2 + 50;
            var roll = ThreadSafeRandom.Next(0.0f, 1.0f);

            var chance3 = SkillCheck.GetSkillChance(appraisalSkill.Current, diff3);
            if (chance3 > roll)
            {
                Proficiency.OnSuccessUse(player, appraisalSkill, diff3);
                return (uint)Math.Max(Math.Ceiling(cost * 0.85f), 1);
            }

            var chance2 = SkillCheck.GetSkillChance(appraisalSkill.Current, diff2);
            if (chance2 > roll)
            {
                Proficiency.OnSuccessUse(player, appraisalSkill, diff2);
                return (uint)Math.Max(Math.Ceiling(cost * 0.90f), 1);
            }

            var chance1 = SkillCheck.GetSkillChance(appraisalSkill.Current, diff1);
            if (chance1 > roll)
            {
                Proficiency.OnSuccessUse(player, appraisalSkill, diff1);
                return (uint)Math.Max(Math.Ceiling(cost * 0.95f), 1);
            }

            return cost;
        }

        public uint GetSellCost(WorldObject item) => GetSellCost(item.Value, item.ItemType);

        public uint GetSellCost(Weenie item) => GetSellCost(item.GetValue(), item.GetItemType());

        private uint GetSellCost(int? value, ItemType? itemType)
        {
            var sellRate = SellPrice ?? 1.0;
            if (itemType == ItemType.PromissoryNote)
                sellRate = 1.15;

            var cost = Math.Max(1, (uint)Math.Ceiling(((float)sellRate * (value ?? 0)) - 0.1));
            return cost;
        }

        public int GetBuyCost(WorldObject item) => GetBuyCost(item.Value, item.ItemType);

        public int GetBuyCost(Weenie item) => GetBuyCost(item.GetValue(), item.GetItemType());

        private int GetBuyCost(int? value, ItemType? itemType)
        {
            var buyRate = BuyPrice ?? 1;
            if (itemType == ItemType.PromissoryNote)
                buyRate = 1.0;

            var cost = Math.Max(1, (int)Math.Floor(((float)buyRate * (value ?? 0)) + 0.1));
            return cost;
        }

        public int CalculatePayoutCoinAmount(Dictionary<uint, WorldObject> items, Player player, bool performAppraisal)
        {
            var payout = 0;

            foreach (WorldObject item in items.Values)
            {
                if (performAppraisal && Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                    MagnifyingGlass.PerformAppraisal(player, item, true);
                payout += GetBuyCost(item);
            }

            return payout;
        }

        /// <summary>
        /// This will either add the item to the vendors temporary sellables, or destroy it.<para />
        /// In both cases, the item will be removed from the database.<para />
        /// The item should already have been removed from the players inventory
        /// </summary>
        public void ProcessItemsForPurchase(Player player, Dictionary<uint, WorldObject> items)
        {
            foreach (var item in items.Values)
            {
                var resellItem = true;

                // don't resell DestroyOnSell
                if (item.GetProperty(PropertyBool.DestroyOnSell) ?? false)
                    resellItem = false;

                // don't resell Attuned items that can be sold
                if (item.Attuned == AttunedStatus.Attuned)
                    resellItem = false;

                // don't resell stackables?
                if (item.MaxStackSize != null || item.MaxStructure != null)
                    resellItem = false;

                if (resellItem)
                {
                    item.ContainerId = Guid.Full;

                    if (!UniqueItemsForSale.TryAdd(item.Guid, item))
                    {
                        var sellItems = string.Join(", ", items.Values.Select(i => $"{i.Name} ({i.Guid})"));
                        log.Error($"[VENDOR] {Name}.ProcessItemsForPurchase({player.Name}): duplicate item found, sell list: {sellItems}");
                    }

                    item.SoldTimestamp = Time.GetUnixTime();

                    // verify no gap: even though the guid is technically free in the database at this point,
                    // is it still marked as consumed in guid manager, and not marked as freed here?
                    // if player repurchases item sometime later, we must ensure the guid is still marked as consumed for re-add

                    // remove object from shard db, but keep a reference to it in memory
                    // for DestroyOnSell items, these will effectively be destroyed immediately
                    // for other items, if a player re-purchases, it will be added to the shard db again
                    item.RemoveBiotaFromDatabase();
                }
                else
                    item.Destroy();

                NumItemsBought++;
            }

            ApproachVendor(player, VendorType.Sell);
        }

        public void ApplyService(WorldObject item, Player target)
        {
            // verify -- players purchasing multiple services in 1 transaction, and IsBusy state?
            var spell = new Spell(item.SpellDID ?? 0);

            if (spell.NotFound)
                return;

            IsBusy = true;

            var preCastTime = PreCastMotion(target);

            var castChain = new ActionChain();
            castChain.AddDelaySeconds(preCastTime);
            castChain.AddAction(this, () =>
            {
                TryCastSpell(spell, target, this);
                PostCastMotion();
            });

            var postCastTime = GetPostCastTime(spell);

            castChain.AddDelaySeconds(postCastTime);
            castChain.AddAction(this, () => IsBusy = false);

            castChain.EnqueueChain();

            NumServicesSold++;
        }

        private int ShopTier = 0;
        private int ShopRandomItemStockAmount = 0;
        private float ShopQualityMod = 0.0f;
        private bool RandomItemGenerationInitialized = false;
        private bool IsStarterOutpostVendor = false;

        private bool sellsRandomArmor;
        private bool sellsRandomMeleeWeapons;
        private bool sellsRandomMissileWeapons;
        private bool sellsRandomCasters;
        private bool sellsRandomClothing;
        private bool sellsRandomJewelry;
        private bool sellsRandomGems;
        private bool sellsRandomScrolls;
        private bool sellsSalvage;
        private bool sellsSpecialItems;

        private TreasureHeritageGroup ShopHeritage;

        private void SetupRandomItemShop()
        {
            RandomItemGenerationInitialized = true;

            ShopTier = Tier ?? 0;

            string townName = GetProperty(PropertyString.TownName);
            switch (townName)
            {
                case "South Holtburg Outpost":
                case "West Holtburg Outpost":
                case "East Lytelthorpe Outpost":
                case "West Lytelthorpe Outpost":
                case "East Rithwic Outpost":
                case "South Rithwic Outpost":
                case "East Nanto Outpost":
                case "North Nanto Outpost":
                case "Southeast Shoushi Outpost":
                case "West Shoushi Outpost":
                case "North Yanshi Outpost":
                case "South Yanshi Outpost":
                case "North Al-Arqas Outpost":
                case "West Al-Arqas Outpost":
                case "East Samsur Outpost":
                case "Northwest Samsur Outpost":
                case "East Yaraq Outpost":
                case "North Yaraq Outpost":
                    if(ShopTier == 0)
                        ShopTier = 1;
                    ShopQualityMod = -0.5f;
                    IsStarterOutpostVendor = true;
                    break;
                case "Al-Arqas":
                case "Bluespire":
                case "Greenspire":
                case "Holtburg":
                case "Lytelthorpe":
                case "Martine's Retreat":
                case "Nanto":
                case "Redspire":
                case "Rithwic":
                case "Samsur":
                case "Shoushi":
                case "Tufa":
                case "Underground City":
                case "Xarabydun":
                case "Yanshi":
                case "Yaraq":
                // The following 3 outposts are not starter outposts
                case "East Glenden Wood Outpost":
                case "West Glenden Wood Outpost":
                case "Southwest Yanshi Outpost":
                    if (ShopTier == 0)
                        ShopTier = 1;
                    ShopQualityMod = 0.0f;
                    break;
                case "Al-Jalima":
                case "Ahurenga":
                case "Arwic":
                case "Baishi":
                case "Cragstone":
                case "Eastham":
                case "Glenden Wood":
                case "Hebian-to":
                case "Khayyaban":
                case "Kryst":
                case "Lin":
                case "Mayoi":
                case "Oolutanga's Refuge":
                case "Sawato":
                case "Tou-Tou":
                case "Uziz":
                case "Zaikhal":
                    if (ShopTier == 0)
                        ShopTier = 2;
                    ShopQualityMod = 0.0f;
                    break;
                case "Bandit Castle":
                case "Crater Lake":
                case "Dryreach":
                case "Danby's Outpost":
                case "Kara":
                case "Linvak Tukal":
                case "MacNiall's Freehold":
                case "Neydisa Castle":
                case "Plateau":
                case "Qalaba'r":
                case "Timaru":
                    if (ShopTier == 0)
                        ShopTier = 3;
                    ShopQualityMod = 0.0f;
                    break;
                case "Candeth Keep":
                case "Fort Tethana":
                case "Ayan Baqur":
                case "WaiJhou":
                case "Stonehold":
                    if (ShopTier == 0)
                        ShopTier = 4;
                    ShopQualityMod = 0.0f;
                    break;
            }

            sellsRandomArmor = ((ItemType)MerchandiseItemTypes & ItemType.Armor) == ItemType.Armor;
            sellsRandomMeleeWeapons = ((ItemType)MerchandiseItemTypes & ItemType.MeleeWeapon) == ItemType.MeleeWeapon;
            sellsRandomMissileWeapons = ((ItemType)MerchandiseItemTypes & ItemType.MissileWeapon) == ItemType.MissileWeapon;
            sellsRandomCasters = ((ItemType)MerchandiseItemTypes & ItemType.Caster) == ItemType.Caster;
            sellsSalvage = VendorSellsSalvage;
            sellsSpecialItems = VendorSellsSpecialItems;

            if (!IsStarterOutpostVendor)
            {
                sellsRandomClothing = ((ItemType)MerchandiseItemTypes & ItemType.Clothing) == ItemType.Clothing;
                sellsRandomJewelry = ((ItemType)MerchandiseItemTypes & ItemType.Jewelry) == ItemType.Jewelry;
                sellsRandomGems = ((ItemType)MerchandiseItemTypes & ItemType.Gem) == ItemType.Gem;
                sellsRandomScrolls = ((ItemType)MerchandiseItemTypes & ItemType.Writable) == ItemType.Writable && sellsRandomCasters; // Check if we also sell casters to prevent scribes from carrying scrolls
            }

            int categoriesSold = 0;
            if (sellsRandomArmor)
                categoriesSold++;
            if (sellsRandomMeleeWeapons)
                categoriesSold++;
            if (sellsRandomMissileWeapons)
                categoriesSold++;
            if (sellsRandomCasters)
                categoriesSold++;
            if (sellsRandomClothing)
                categoriesSold++;
            if (sellsRandomJewelry)
                categoriesSold++;
            if (sellsRandomGems)
                categoriesSold++;
            if (sellsRandomScrolls)
                categoriesSold++;
            if (sellsSalvage)
                categoriesSold++;
            if (sellsSpecialItems)
                categoriesSold++;

            if (VendorStockMaxAmount == 0)
                ShopRandomItemStockAmount = categoriesSold * ThreadSafeRandom.Next(5, 10);
            else
                ShopRandomItemStockAmount = categoriesSold * VendorStockMaxAmount;

            ShopHeritage = (TreasureHeritageGroup)(Heritage ?? 0);

            if (ShopHeritage > TreasureHeritageGroup.Sho)
                ShopHeritage = TreasureHeritageGroup.Invalid;

            if (ShopTier == 0) // We're not in a town and no defined shop tier! See what's around us.
            {
                ShopTier = 1; // Fallback to tier 1 if there's nothing around us.

                foreach (var obj in CurrentLandblock.GetAllWorldObjectsForDiagnostics())
                {
                    if(obj.ItemType == ItemType.Creature)
                    {
                        PlayerKillerStatus pkStatus = (PlayerKillerStatus)(obj.GetProperty(PropertyInt.PlayerKillerStatus) ?? 0);

                        Creature creature = obj as Creature;

                        if (!(obj.Guid.IsPlayer()) && creature != null && pkStatus != PlayerKillerStatus.RubberGlue)
                        {
                            if(creature.DeathTreasure != null && creature.DeathTreasure.Tier > ShopTier)
                                ShopTier = creature.DeathTreasure.Tier; // Trade in the highest tier we can find around us.
                        }
                    }
                }
            }

            // Let's overwrite the database values for MerchandiseMaxValue depending on our tier.
            switch (ShopTier)
            {
                default:
                case 1:
                    if (IsStarterOutpostVendor)
                        MerchandiseMaxValue = 1000;
                    else
                        MerchandiseMaxValue = 3000;
                    break;
                case 2:
                    MerchandiseMaxValue = 12000;
                    break;
                case 3:
                    MerchandiseMaxValue = 18000;
                    break;
                case 4:
                    MerchandiseMaxValue = 30000;
                    break;
                case 5:
                    MerchandiseMaxValue = 40000;
                    break;
                case 6:
                    MerchandiseMaxValue = 50000;
                    break;
            }
        }

        private void RestockRandomItems()
        {
            if (!RandomItemGenerationInitialized)
                SetupRandomItemShop();

            if (ShopTier == 0)
                return;

            if (VendorRestockInterval == 0 && (DateTime.UtcNow - LastRestockTime).TotalSeconds < PropertyManager.GetDouble("vendor_unique_rot_time", 300).Item)
                return;
            else if ((DateTime.UtcNow - LastRestockTime).TotalSeconds < VendorRestockInterval)
                return;

            RotUniques();

            // Decay our recent income and outflow so prices can change accordingly.
            int minutesSinceLastRestock = (int)(DateTime.UtcNow - LastRestockTime).TotalMinutes;
            int decayAmount = ThreadSafeRandom.Next(VendorHappyMean ?? 125, VendorHappyMean ?? 125 + VendorHappyVariance ?? 125) * minutesSinceLastRestock;
            RecentMoneyOutflow = Math.Max(RecentMoneyOutflow - decayAmount, 0);
            RecentMoneyIncome = Math.Max(RecentMoneyIncome - decayAmount, 0);

            LastRestockTime = DateTime.UtcNow;

            UpdateHappyVendor();

            if (UniqueItemsForSale.Count >= ShopRandomItemStockAmount)
                return;

            int itemsToGenerate = ShopRandomItemStockAmount - UniqueItemsForSale.Count;

            if (itemsToGenerate <= 0)
                return;

            int added = 0;
            while(added < itemsToGenerate)
            {
                if (sellsRandomArmor && added < itemsToGenerate)
                {
                    AddRandomItem(TreasureItemType_Orig.Armor, TreasureArmorType.Undef, TreasureWeaponType.Undef);
                    added++;
                }
                if (sellsRandomMeleeWeapons && added < itemsToGenerate)
                {
                    AddRandomItem(TreasureItemType_Orig.Weapon, TreasureArmorType.Undef, TreasureWeaponType.MeleeWeapon);
                    added++;
                }
                if (sellsRandomMissileWeapons && added < itemsToGenerate)
                {
                    AddRandomItem(TreasureItemType_Orig.Weapon, TreasureArmorType.Undef, TreasureWeaponType.MissileWeapon);
                    added++;
                }
                if (sellsRandomCasters && added < itemsToGenerate)
                {
                    AddRandomItem(TreasureItemType_Orig.Caster, TreasureArmorType.Undef, TreasureWeaponType.Undef);
                    added++;
                }
                if (sellsRandomClothing && added < itemsToGenerate)
                {
                    AddRandomItem(TreasureItemType_Orig.Clothing, TreasureArmorType.Undef, TreasureWeaponType.Undef);
                    added++;
                    if (added < itemsToGenerate)
                    {
                        AddRandomItem(TreasureItemType_Orig.Armor, TreasureArmorType.Cloth, TreasureWeaponType.Undef);
                        added++;
                    }
                }
                if (sellsRandomJewelry && added < itemsToGenerate)
                {
                    AddRandomItem(TreasureItemType_Orig.Jewelry, TreasureArmorType.Undef, TreasureWeaponType.Undef);
                    added++;
                }
                if (sellsRandomGems && added < itemsToGenerate)
                {
                    AddRandomItem(TreasureItemType_Orig.Gem, TreasureArmorType.Undef, TreasureWeaponType.Undef);
                    added++;
                }
                if (sellsRandomScrolls && added < itemsToGenerate)
                {
                    AddRandomItem(TreasureItemType_Orig.Scroll, TreasureArmorType.Undef, TreasureWeaponType.Undef);
                    added++;
                }
                if (sellsSalvage && added < itemsToGenerate)
                {
                    AddRandomItem(TreasureItemType_Orig.Salvage, TreasureArmorType.Undef, TreasureWeaponType.Undef);
                    added++;
                }
                if (sellsSpecialItems && added < itemsToGenerate)
                {
                    AddRandomItem(TreasureItemType_Orig.SpecialItem, TreasureArmorType.Undef, TreasureWeaponType.Undef);
                    added++;
                }
            }

            UniqueItemsForSale = new Dictionary<ObjectGuid, WorldObject>(UniqueItemsForSale.OrderBy(key => key.Value, VendorItemComparer));
        }
        public void BroadcastStock(bool world = false)
        {
            RestockRandomItems();

            if (UniqueItemsForSale.Count == 0)
                return;

            var message = GetProperty(PropertyString.VendorBroadcastPrepend) ?? "";
            if (message.Length == 0)
                message = $"{Name} is now selling";

            var itemsForSale = new Dictionary<uint, WorldObject>();

            // Concatenate non mutated entries.
            foreach(var itemEntry in UniqueItemsForSale)
            {
                var item = itemEntry.Value;
                if (item.Workmanship.HasValue || !itemsForSale.ContainsKey(item.WeenieClassId))
                    itemsForSale.Add(item.WeenieClassId, item);
            }

            for (int i = 0; i < itemsForSale.Count; i++)
            {
                var item = itemsForSale.Values.ElementAt(i);
                int value;
                if (item.MaxStackSize > 1)
                    value = item.StackUnitValue ?? 0;
                else
                    value = item.Value ?? 0;

                if (i == 0)
                    message = $"{message} {item.NameWithMaterial}{(item.ItemType == ItemType.TinkeringMaterial ? $" (Workmanship {(item.Workmanship ?? 0):#.00})" : "")} for {(int)Math.Round(value * SellPrice ?? 0):N0} Pyreals";
                else if (i == itemsForSale.Count - 1)
                    message = $"{message} and {item.NameWithMaterial}{(item.ItemType == ItemType.TinkeringMaterial ? $" (Workmanship {(item.Workmanship ?? 0):#.00})" : "")} for {(int)Math.Round(value * SellPrice ?? 0):N0} Pyreals";
                else
                    message = $"{message}, {item.NameWithMaterial}{(item.ItemType == ItemType.TinkeringMaterial ? $" (Workmanship {(item.Workmanship ?? 0):#.00})" : "")} for {(int)Math.Round(value * SellPrice ?? 0):N0} Pyreals";
            }
            var append = GetProperty(PropertyString.VendorBroadcastAppend) ?? "";
            if(append.Length > 0)
                message += append;
            else
                message += ".";

            if (world)
            {
                PlayerManager.BroadcastToAll(new GameMessageSystemChat(message, ChatMessageType.WorldBroadcast));
                PlayerManager.LogBroadcastChat(Channel.AllBroadcast, this, message);
            }
            else
            {
                EnqueueBroadcast(new GameMessageSystemChat(message, ChatMessageType.Broadcast));
            }
        }

        private void AddRandomItem(TreasureItemType_Orig treasureItemType, TreasureArmorType armorType = TreasureArmorType.Undef, TreasureWeaponType weaponType = TreasureWeaponType.Undef)
        {
            var item = LootGenerationFactory.CreateRandomLootObjects_New(ShopTier, ShopQualityMod, (DealMagicalItems ?? false) ? TreasureItemCategory.MagicItem : TreasureItemCategory.Item, treasureItemType, armorType, weaponType, ShopHeritage);

            var amount = item.StackSize ?? 1;
            if (amount > 1 && !item.Workmanship.HasValue) // Split stackable uniques.
            {
                for (int i = 0; i < amount; i++)
                {
                    var newItem = WorldObjectFactory.CreateNewWorldObject(item.WeenieClassId);
                    newItem.ContainerId = Guid.Full;

                    UniqueItemsForSale.Add(newItem.Guid, newItem);

                    newItem.SoldTimestamp = Time.GetUnixTime();
                    newItem.RemoveBiotaFromDatabase();
                }
                item.Destroy();
            }
            else
            {
                item.ContainerId = Guid.Full;

                UniqueItemsForSale.Add(item.Guid, item);

                item.SoldTimestamp = Time.GetUnixTime();

                item.RemoveBiotaFromDatabase();
            }
        }

        /// <summary>
        /// Unique items in the vendor's inventory sold to the vendor by players
        /// expire after vendor_unique_rot_time seconds
        /// </summary>
        private void RotUniques()
        {
            List<WorldObject> itemsToRemove = null;

            foreach (var uniqueItem in UniqueItemsForSale.Values)
            {
                var soldTime = uniqueItem.SoldTimestamp;

                if (soldTime == null)
                {
                    log.Warn($"[VENDOR] Vendor {Name} has unique item {uniqueItem.Name} ({uniqueItem.Guid}) without a SoldTimestamp -- this shouldn't happen");
                    continue;   // keep in list?
                }

                var rotTime = Time.GetDateTimeFromTimestamp(soldTime.Value);

                if (VendorStockTimeToRot != 0)
                    rotTime = rotTime.AddSeconds(VendorStockTimeToRot);
                else
                    rotTime = rotTime.AddSeconds(PropertyManager.GetDouble("vendor_unique_rot_time", 300).Item);

                if (DateTime.UtcNow >= rotTime)
                {
                    if (itemsToRemove == null)
                        itemsToRemove = new List<WorldObject>();

                    itemsToRemove.Add(uniqueItem);
                }
            }
            if (itemsToRemove != null)
            {
                foreach (var itemToRemove in itemsToRemove)
                {
                    log.Debug($"[VENDOR] Vendor {Name} has discontinued sale of {itemToRemove.Name} and removed it from its UniqueItemsForSale list.");
                    UniqueItemsForSale.Remove(itemToRemove.Guid);

                    itemToRemove.Destroy();     // even though it has already been removed from the db at this point, we want to mark as freed in guid manager now
                }
            }
        }

        private static void CleanupCreatedItems(List<WorldObject> createdItems)
        {
            foreach (var createdItem in createdItems)
                createdItem.Destroy();
        }

        public bool OpenForBusiness
        {
            get => GetProperty(PropertyBool.OpenForBusiness) ?? true;
            set { if (value) RemoveProperty(PropertyBool.OpenForBusiness); else SetProperty(PropertyBool.OpenForBusiness, value); }
        }

        //public int? MerchandiseItemTypes
        //{
        //    get => GetProperty(PropertyInt.MerchandiseItemTypes);
        //    set { if (!value.HasValue) RemoveProperty(PropertyInt.MerchandiseItemTypes); else SetProperty(PropertyInt.MerchandiseItemTypes, value.Value); }
        //}

        public int? MerchandiseMinValue
        {
            get => GetProperty(PropertyInt.MerchandiseMinValue);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.MerchandiseMinValue); else SetProperty(PropertyInt.MerchandiseMinValue, value.Value); }
        }

        public int? MerchandiseMaxValue
        {
            get => GetProperty(PropertyInt.MerchandiseMaxValue);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.MerchandiseMaxValue); else SetProperty(PropertyInt.MerchandiseMaxValue, value.Value); }
        }

        public double? BuyPrice
        {
            get { return BuyPriceBase != null ? BuyPriceBase + BuyPriceMod : null; }
        }

        public double? SellPrice
        {
            get { return SellPriceBase != null ? SellPriceBase + SellPriceMod : null; }
        }

        public double? BuyPriceBase
        {
            get => GetProperty(PropertyFloat.BuyPrice);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.BuyPrice); else SetProperty(PropertyFloat.BuyPrice, value.Value); }
        }

        public double? SellPriceBase
        {
            get => GetProperty(PropertyFloat.SellPrice);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.SellPrice); else SetProperty(PropertyFloat.SellPrice, value.Value); }
        }

        public bool? DealMagicalItems
        {
            get => GetProperty(PropertyBool.DealMagicalItems);
            set { if (!value.HasValue) RemoveProperty(PropertyBool.DealMagicalItems); else SetProperty(PropertyBool.DealMagicalItems, value.Value); }
        }

        public bool? VendorService
        {
            get => GetProperty(PropertyBool.VendorService);
            set { if (!value.HasValue) RemoveProperty(PropertyBool.VendorService); else SetProperty(PropertyBool.VendorService, value.Value); }
        }

        public int? VendorHappyMean
        {
            get => GetProperty(PropertyInt.VendorHappyMean);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.VendorHappyMean); else SetProperty(PropertyInt.VendorHappyMean, value.Value); }
        }

        public int? VendorHappyVariance
        {
            get => GetProperty(PropertyInt.VendorHappyVariance);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.VendorHappyVariance); else SetProperty(PropertyInt.VendorHappyVariance, value.Value); }
        }

        public int? VendorHappyMaxItems
        {
            get => GetProperty(PropertyInt.VendorHappyMaxItems);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.VendorHappyMaxItems); else SetProperty(PropertyInt.VendorHappyMaxItems, value.Value); }
        }

        public int NumItemsSold
        {
            get => GetProperty(PropertyInt.NumItemsSold) ?? 0;
            set { if (value == 0) RemoveProperty(PropertyInt.NumItemsSold); else SetProperty(PropertyInt.NumItemsSold, value); }
        }

        public int NumItemsBought
        {
            get => GetProperty(PropertyInt.NumItemsBought) ?? 0;
            set { if (value == 0) RemoveProperty(PropertyInt.NumItemsBought); else SetProperty(PropertyInt.NumItemsBought, value); }
        }

        public int NumServicesSold
        {
            get => GetProperty(PropertyInt.NumServicesSold) ?? 0;
            set { if (value == 0) RemoveProperty(PropertyInt.NumServicesSold); else SetProperty(PropertyInt.NumServicesSold, value); }
        }

        public int MoneyIncome
        {
            get => GetProperty(PropertyInt.MoneyIncome) ?? 0;
            set { if (value == 0) RemoveProperty(PropertyInt.MoneyIncome); else SetProperty(PropertyInt.MoneyIncome, value); }
        }

        public int MoneyOutflow
        {
            get => GetProperty(PropertyInt.MoneyOutflow) ?? 0;
            set { if (value == 0) RemoveProperty(PropertyInt.MoneyOutflow); else SetProperty(PropertyInt.MoneyOutflow, value); }
        }

        public int RecentMoneyIncome
        {
            get => GetProperty(PropertyInt.RecentMoneyIncome) ?? 0;
            set { if (value == 0) RemoveProperty(PropertyInt.RecentMoneyIncome); else SetProperty(PropertyInt.RecentMoneyIncome, value); }
        }

        public int RecentMoneyOutflow
        {
            get => GetProperty(PropertyInt.RecentMoneyOutflow) ?? 0;
            set { if (value == 0) RemoveProperty(PropertyInt.RecentMoneyOutflow); else SetProperty(PropertyInt.RecentMoneyOutflow, value); }
        }

        public bool VendorSellsSalvage
        {
            get => GetProperty(PropertyBool.VendorSellsSalvage) ?? false;
            set { if (value == false) RemoveProperty(PropertyBool.VendorSellsSalvage); else SetProperty(PropertyBool.VendorSellsSalvage, value); }
        }

        public bool VendorSellsSpecialItems
        {
            get => GetProperty(PropertyBool.VendorSellsSpecialItems) ?? false;
            set { if (value == false) RemoveProperty(PropertyBool.VendorSellsSpecialItems); else SetProperty(PropertyBool.VendorSellsSpecialItems, value); }
        }

        public bool VendorPKOnly
        {
            get => GetProperty(PropertyBool.VendorPKOnly) ?? false;
            set { if (value == false) RemoveProperty(PropertyBool.VendorPKOnly); else SetProperty(PropertyBool.VendorPKOnly, value); }
        }

        protected double VendorRestockInterval
        {
            get => GetProperty(PropertyFloat.VendorRestockInterval) ?? 0;
            set { if (value == 0) RemoveProperty(PropertyFloat.VendorRestockInterval); else SetProperty(PropertyFloat.VendorRestockInterval, value); }
        }

        protected double VendorStockTimeToRot
        {
            get => GetProperty(PropertyFloat.VendorStockTimeToRot) ?? 0;
            set { if (value == 0) RemoveProperty(PropertyFloat.VendorStockTimeToRot); else SetProperty(PropertyFloat.VendorStockTimeToRot, value); }
        }

        protected int VendorStockMaxAmount
        {
            get => GetProperty(PropertyInt.VendorStockMaxAmount) ?? 0;
            set { if (value == 0) RemoveProperty(PropertyInt.VendorStockMaxAmount); else SetProperty(PropertyInt.VendorStockMaxAmount, value); }
        }
    }
}
