﻿using TbsCrossPlatform.Models.Enums;

namespace TbsCrossPlatform.Models
{
    public class HeroItem
    {
        public int AccountId { get; set; }
        public HeroItemEnum Item { get; set; }
        public int Count { get; set; }
    }
}