/* Copyright (c) 2018 Rebecca Ramnauth */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace xml_converter
{
    class Regulation
    {
        private readonly string file;
        private readonly XDocument xdoc = null;
        private List<string> file_items = new List<string>();
        private List<string> content_items = new List<string>();
        private List<string> user_items = new List<string>();
        private List<string> regulation_items = new List<string>();

        public Regulation(string f, int n)
        {
            file = f;
            if (f != null) { xdoc = XDocument.Load(file); }
            regulation_items = Enumerable.Repeat("", n).ToList();
        }

        // setters
        public void AddFileItem(string item) { file_items.Add(item); }
        public void AddContentItem(string item) { content_items.Add(item); }
        public void AddUserItem(string item) { user_items.Add(item); }
        public void SetRegulationItem(int index, string item) { regulation_items[index] = item; }

        // getters
        public string GetFileItem(int index) { return file_items[index]; }
        public string GetContentItem(int index) { return content_items[index]; }
        public string GetUserItem(int index) { return user_items[index]; }
        public string GetRegulationItem(int index) { return regulation_items[index]; }

        public string ParseByKey(List<string> keys)
        {
            int type = keys.Count;
            //XDocument xdoc = XDocument.Load(file);
            IEnumerable<XElement> els;
            string val = "";

            switch (type)
            {
                case 1:
                    els = xdoc.Descendants().Where(p => p.Name == keys[0]);
                    break;
                case 2:
                    els = new List<XElement>();
                    foreach (XElement el in xdoc.Descendants(keys[0]))
                        val = (string)el.Attribute(keys[1]) ?? val;
                    break;
                case 3:
                    els = xdoc.Descendants().Where(p => p.Name == keys[0] && p.Attribute(keys[1]).Value == keys[2]);
                    break;
                case 4:
                    els = xdoc.Descendants().Where(p => p.Name == keys[0] && p.Attribute(keys[1]).Value == keys[2]).Elements(keys[3]);
                    break;
                default:
                    els = new List<XElement>();
                    break;
            }

            try { foreach (XElement el in els) { val += el.Value + " "; } }
            catch (Exception e) {  Globals.LOG.AppendException(e, false); } // misargument exception

            return val;
        }
    }
}