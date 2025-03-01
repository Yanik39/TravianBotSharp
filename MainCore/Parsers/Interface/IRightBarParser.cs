﻿using HtmlAgilityPack;

namespace MainCore.Parser.Interface
{
    public interface IRightBarParser
    {
        public bool HasPlusAccount(HtmlDocument doc);

        public int GetTribe(HtmlDocument doc);
    }
}