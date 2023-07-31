using System;

namespace ACE.Entity.Models
{
    public class ConsignmentComplete
    {
        public uint Id { get; set; }
        public ObjectGuid ObjectId { get; set; }
        public uint WeenieClassId { get; set; }
        public ObjectGuid SellerId { get; set; }
        public ObjectGuid? BuyerId { get; set; }
        public uint Price { get; set; }
        public uint? SoldTime { get; set; }

        public Biota Item { get; set; }
        public Biota Box { get; set; }
    }
}
