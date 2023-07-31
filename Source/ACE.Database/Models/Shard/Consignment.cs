using System;
using System.Collections.Generic;

#nullable disable

namespace ACE.Database.Models.Shard
{
    public partial class Consignment
    {
        public uint Id { get; set; }
        public uint ObjectId { get; set; }
        public uint SellerId { get; set; }
        public uint Price { get; set; }
        public int OriginalValue { get; set; }
        public uint ListTime { get; set; }
        public uint ExpireTime { get; set; }

        public virtual Biota Object { get; set; }
        public virtual Character Seller { get; set; }
    }
}
