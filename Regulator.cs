/* Copyright (c) 2018 Rebecca Ramnauth */

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace xml_converter
{
    class Regulator
    {
        private readonly string dir;

        private List<string> reg_definition;      // regulation definitions
        private List<string> file_definition;     // file definitions
        private List<string> content_definition;  // content definitions
        private List<string> user_content;        // user definitions

        public Regulator(string d, List<string> user_meta, List<string> file_meta, List<string> content_meta, List<string> reg_meta)
        {
            dir = d + "/" + user_meta[0] + "/";
            user_content = user_meta;
            reg_definition = reg_meta;
            file_definition = file_meta;
            content_definition = content_meta;
        }

        // https://blogs.msdn.microsoft.com/xmlteam/2011/09/14/effective-xml-part-1-choose-the-right-api/
        /*  XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;

            var reader = new XmlTextReader(file);
            reader.MoveToContent();
            reader.Read();
            while (reader.NodeType == XmlNodeType.Element)
            {
                switch (reader.Name)
                {...} ... }
                            
         * "Avoid using XmlTextReader. It contains quite a few bugs that could not 
         * be fixed without breaking existing applications already using it."
         */
        IEnumerable<XElement> StreamAxis(string inputUrl, string matchName)
        {
            using (var stream = File.Open(inputUrl, FileMode.Open, FileAccess.Read))
            {
                using (XmlReader reader = XmlReader.Create(stream))
                {
                    reader.MoveToContent();
                    while (reader.Read())
                    {
                        switch (reader.NodeType)
                        {
                            case XmlNodeType.Element:
                                if (matchName.Contains(reader.Name) && matchName.Contains(reader.NamespaceURI))
                                {
                                    if (XNode.ReadFrom(reader) is XElement el) yield return el;
                                }
                                break;
                        }
                    }
                    //reader.Close();
                }
            }
        }

        public bool ParseReg()
        {
            bool feedback = true;
            string status_msg = "";

            try
            {
                Globals.LOG.AppendLocation();
                List<Regulation> regs = new List<Regulation>();
                List<string> file_content;
                //List<List<string>> content_content;
                List<List<string>> keys = InterpretRegDefinition();

                SqlCommand SqlComm;
                
                // identify feed
                //string file = Directory.EnumerateFiles(dir, "*-1.xml").Last();
                string file = dir + "/" + (from f in new DirectoryInfo(dir).GetFiles("*-1.xml")
                    orderby f.LastWriteTime descending
                    select f).First().ToString();
                string origin_num = Path.GetFileNameWithoutExtension(GetOriginPath(file, 1));
                Console.WriteLine("\n\t text = " + origin_num);

                // record file start
                Guid file_g = Guid.NewGuid();
                string origin_name = Path.GetFileNameWithoutExtension(file);
                SqlComm = new SqlCommand("INSERT INTO " + Globals.FILE_TABLE + "(Tbl_id, Subscription_id, File_name, Start_Time) VALUES(@guid, @sub, @file, @start)", Globals.CONNECTION);
                SqlComm.Parameters.AddWithValue("@guid", file_g);
                SqlComm.Parameters.AddWithValue("@sub", user_content[0]);
                SqlComm.Parameters.AddWithValue("@file", origin_num);
                SqlComm.Parameters.AddWithValue("@start", DateTime.Now.ToString("yyyy-MM-dd h:mm:ss.ff tt"));
                SqlComm.ExecuteNonQuery();

                try
                {
                    file_content = InterpretTable(file);

                    //https://docs.microsoft.com/en-us/dotnet/api/system.xml.linq.xdocument.load?view=netframework-4.8
                    XDocument xdoc = XDocument.Load(file);
                    IEnumerable<XElement> entries = xdoc.Descendants(file_definition[file_definition.Count - 1]); // tag "entry"
                    //IEnumerable<XElement> entries = StreamAxis(file, file_definition[file_definition.Count - 1]);

                    if (file_content.Count <= 0 || entries.Count() <= 0)
                    {
                        Globals.LOG.AppendMessage("Consider defining a new structure for " + user_content[0] + "/" + Path.GetFileName(file));
                        status_msg = "Consider defining new structure";
                    }
                    else
                    {
                        int entry_index = 0;
                        int entry_num = 2;

                        foreach (XElement entry in entries)
                        {
                            try
                            {
                                List<string> entry_meta = new List<string>();
                                for (int i = 0; i < content_definition.Count; i++) { entry_meta.Add(entries.Descendants(content_definition[i]).ElementAt(entry_index).Value); }

                                Regulation r;
                                if (entry_meta[2].Contains(Globals.DELETE_VAL)) // marked for deletion
                                {
                                    Console.Write("\t\t" + entry_meta[0] + " --> " + entry_meta[2]);
                                    r = new Regulation(null, keys.Count);

                                    // fill fields
                                    foreach (string item in file_content) { r.AddFileItem(item); }
                                    foreach (string item in entry_meta) { r.AddContentItem(item); }
                                    foreach (string item in user_content) { r.AddUserItem(item); }
                                    for (int j = 0; j < keys.Count; j++) { r.SetRegulationItem(j, ""); }

                                    try { Console.Write(" --> " + Program.CreateInsertCommand(Globals.CONTENT_TABLE, Globals.RELATIONSHIP_MAP, r, Globals.CONNECTION).ExecuteNonQuery()); Console.Write(" --> commit delete item \n"); }
                                    catch (Exception e) { Globals.LOG.AppendException(e, false); Console.Write(" --> delete item caused " + e.GetType() + "\n"); }
                                }
                                else
                                {
                                    // generate path
                                    string entry_path = dir + origin_num + "-" + entry_num + ".xml";
                                    string content_id = entry_meta[0];

                                    r = new Regulation(entry_path, keys.Count);

                                    // fill fields
                                    foreach (string item in file_content) { r.AddFileItem(item); }
                                    foreach (string item in entry_meta) { r.AddContentItem(item); }
                                    foreach (string item in user_content) { r.AddUserItem(item); }
                                    for (int j = 0; j < keys.Count; j++) { r.SetRegulationItem(j, r.ParseByKey(keys[j])); }

                                    Console.Write("\t\t xml = " + Path.GetFileName(entry_path) + " --> " + entry_meta[2] + " --> " + r.GetFileItem(1));

                                    // check with delete filter
                                    if (r.GetRegulationItem(12).Contains(content_id)) // delete feed items & misaligned contents
                                    {
                                        try { Console.Write(" --> " + Program.CreateInsertCommand(Globals.CONTENT_TABLE, Globals.RELATIONSHIP_MAP, r, Globals.CONNECTION).ExecuteNonQuery()); Console.Write(" --> commit matched item \n"); }
                                        catch (Exception e) { Globals.LOG.AppendException(e, false); Console.Write(" --> match item caused " + e.GetType() + "\n"); }
                                    }
                                    else if (r.GetRegulationItem(Globals.DELETE_FIL).Trim() != "") // not a feed item
                                    {
                                        Globals.LOG.AppendMessage("Misalignment with feed item " + content_id + " and " + ((r.GetRegulationItem(12).Trim() == "") ? "undefined" : r.GetRegulationItem(12)) + " in " + user_content[0] + "/" + Path.GetFileNameWithoutExtension(entry_path));
                                        status_msg = "Feed-content misalignment starting with " + Path.GetFileName(entry_path);
                                        feedback = false;
                                        Console.Write(" --> non-feed item caused misalignment error \n");
                                    }

                                    // mark as processed
                                    if (Globals.KEEP_XML) { TransferFile(entry_path, Globals.PROCESSED_DIR_XML + "/" + user_content[0] + "/" + Path.GetFileName(entry_path)); }
                                    File.Delete(entry_path);

                                    if (!feedback)
                                    {
                                        //if (Globals.TRANSFER) { TransferFile(GetOriginPath(file, 1), Globals.PROCESSED_DIR_TXT + "/" + user_content[0] + "/" + origin_num + ".txt"); }
                                        if (Globals.KEEP_XML) { TransferFile(file, Globals.PROCESSED_DIR_XML + "/" + user_content[0] + "/" + Path.GetFileName(file)); }
                                        File.Delete(file);
                                        Globals.LOG.AppendMessage("TERMINATING SUBSCRIPTION " + user_content[0] + " PREMATURELY");

                                        SqlComm = new SqlCommand("UPDATE " + Globals.FILE_TABLE + " SET Message=@msg WHERE Tbl_id=@guid", Globals.CONNECTION);
                                        SqlComm.Parameters.AddWithValue("@guid", file_g);
                                        SqlComm.Parameters.AddWithValue("@msg", status_msg);
                                        SqlComm.ExecuteNonQuery();

                                        return false;
                                    }
                                    entry_num++;
                                }
                            }
                            catch (Exception e) { Globals.LOG.AppendException(e, false); Console.WriteLine(e); }
                            entry_index++;
                        }
                        // transfer processed
                        if (Globals.TRANSFER) { TransferFile(GetOriginPath(file, 1), Globals.PROCESSED_DIR_TXT + "/" + user_content[0] + "/" + origin_num + ".txt"); }
                        if (Globals.KEEP_XML) { TransferFile(file, Globals.PROCESSED_DIR_XML + "/" + user_content[0] + "/" + Path.GetFileName(file)); }
                        File.Delete(file);
                    }
                }
                catch (Exception f)
                {
                    //if (Globals.TRANSFER) { TransferFile(GetOriginPath(file, 1), Globals.PROCESSED_DIR_TXT + "/" + user_content[0] + "/" + origin_num + ".txt"); }
                    Globals.LOG.AppendException(f, true); Console.WriteLine(f);
                    status_msg = "Nonfatal:" + f.GetType().ToString();
                }

                // record file end
                SqlComm = new SqlCommand("UPDATE " + Globals.FILE_TABLE + " SET End_Time=@end, Message=@msg WHERE Tbl_id=@guid", Globals.CONNECTION);
                SqlComm.Parameters.AddWithValue("@guid", file_g);
                SqlComm.Parameters.AddWithValue("@end", DateTime.Now.ToString("yyyy-MM-dd h:mm:ss.ff tt"));
                SqlComm.Parameters.AddWithValue("@msg", status_msg);
                SqlComm.ExecuteNonQuery();
            }
            catch (Exception e) { Globals.LOG.AppendException(e, false); }
            return true;
        }

        private string GetOriginPath(string xml_source, int index)
        {
            string source = Path.GetFileNameWithoutExtension(xml_source);
            return (
                Globals.START_DIR + "/" +
                user_content[0] + "/" +
                source.Substring(
                    0, source.Length - ((int)Math.Floor(Math.Log10(index) + 1) + 1)) +
                ".txt"
             );
        }

        private string GetOriginNumber(string path)
        {
            return Path.GetFileNameWithoutExtension(path);
        }

        private void TransferFile(string source, string destination)
        {
            Globals.LOG.AppendLocation();
            if (!Directory.Exists(Path.GetDirectoryName(destination)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
            }
            if (File.Exists(destination))
            {
                File.Delete(destination);
                Globals.LOG.AppendMessage(destination + " was overwritten");
            }
            File.Move(source, destination);
        }

        public List<string> InterpretTable(String file)
        {
            Globals.LOG.AppendLocation();
            List<string> file_content = new List<string>();
            XDocument xdoc = XDocument.Load(file);
            for (int i = 0; i < file_definition.Count - 1; i++)
            {
                try { file_content.Add(xdoc.Descendants(file_definition[i]).FirstOrDefault().Value); }
                catch (Exception e) { Globals.LOG.AppendException(e, false); file_content.Add(" "); }
            }
            return file_content;
        }

        public List<List<string>> InterpretEntries(String file)
        {
            Globals.LOG.AppendLocation();
            List<List<string>> content_content = new List<List<string>>();
            XDocument xdoc = XDocument.Load(file);
            try
            {
                IEnumerable<XElement> entries = xdoc.Descendants(file_definition[file_definition.Count - 1]); // tag "entry"
                int entry_index = 0;
                foreach (XElement entry in entries)
                {
                    List<string> entry_meta = new List<string>();
                    for (int i = 0; i < content_definition.Count; i++)
                    {
                        entry_meta.Add(entries.Descendants(content_definition[i]).ElementAt(entry_index).Value);
                    }
                    content_content.Add(entry_meta);
                    entry_index++;
                }
            }
            catch (Exception e) { Globals.LOG.AppendException(e, false); Console.WriteLine(e); }
            return content_content;
        }

        public List<List<string>> InterpretRegDefinition()
        {
            Globals.LOG.AppendLocation();
            List<List<string>> subformat = new List<List<string>>();
            foreach (string key in reg_definition)
            {
                subformat.Add(Interpret(key));
            }
            return subformat;
        }

        public List<string> Interpret(string key)
        {
            Globals.LOG.AppendLocation();
            List<string> subkeys = new List<string>();
            if (key == null)
            {
                return subkeys;
            }
            else if (key.Contains("/") && key.Contains("=@")) // get element by attribute value of parent element
            {
                subkeys.Add(key.Substring(0, key.IndexOf("["))); // get element name
                subkeys.Add(key.Substring(key.IndexOf("[") + 1, key.IndexOf("=@") - key.IndexOf("[") - 1)); // get attribute name
                subkeys.Add(key.Substring(key.IndexOf("=@") + 2, key.IndexOf("]") - key.IndexOf("=@") - 2)); // get attribute value
                subkeys.Add(key.Substring(key.IndexOf("/") + 1)); // get sub element
            }
            else if (key.Contains("=@")) // get element by attribute value
            {
                subkeys.Add(key.Substring(0, key.IndexOf("["))); // get element name
                subkeys.Add(key.Substring(key.IndexOf("[") + 1, key.IndexOf("=@") - key.IndexOf("[") - 1)); // get attribute name
                subkeys.Add(key.Substring(key.IndexOf("=@") + 2, key.IndexOf("]") - key.IndexOf("=@") - 2)); // get attribute value
            }
            else if (key.Contains("[")) // get element's attribute value
            {
                subkeys.Add(key.Substring(0, key.IndexOf("["))); // get element name
                subkeys.Add(key.Substring(key.IndexOf("[") + 1, key.IndexOf("]") - key.IndexOf("[") - 1)); // get attribute value
            }
            else
            {
                subkeys.Add(key); // get element
            }
            return subkeys;
        }

        private void PrintList(List<List<string>> s)
        {
            foreach (List<string> st in s)
            {
                Globals.LOG.AppendMessage(st.Count + " > " + st.GetType());
                foreach (string str in st) { Globals.LOG.AppendMessage("   " + str); }
            }
        }

        private void PrintList(List<string> s)
        {
            Globals.LOG.AppendMessage(s.Count + " > " + s.GetType());
            foreach (string str in s) { Globals.LOG.AppendMessage("   " + str); }
        }
    }
}
