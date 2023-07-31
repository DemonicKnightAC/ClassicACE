using System;
using System.Collections.Generic;

#nullable disable

namespace ACE.Database.Models.Shard
{
    public partial class ConsignmentComplete
    {
        public uint Id { get; set; }
        public uint ObjectId { get; set; }
        public uint WeenieClassId { get; set; }
        public uint SellerId { get; set; }
        public uint? BuyerId { get; set; }
        public uint Price { get; set; }
        public uint? SoldTime { get; set; }

        public virtual Biota Object { get; set; }
        public virtual Character Seller { get; set; }
        public virtual Character Buyer { get; set; }
    }
}
