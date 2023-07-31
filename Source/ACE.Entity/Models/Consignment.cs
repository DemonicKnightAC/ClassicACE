using System;

namespace ACE.Entity.Models
{
    public class Consignment
    {
        public uint Id { get; set; }
        public ObjectGuid ObjectId { get; set; }
        public ObjectGuid SellerId { get; set; }
        public uint Price { get; set; }
        public int OriginalValue { get; set; }
        public uint ListTime { get; set; }
        public uint ExpireTime { get; set; }

        public Biota Item { get; set; }
        public Biota Box { get; set; }
    }
}
