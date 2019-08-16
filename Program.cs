/* Copyright (c) 2018 Rebecca Ramnauth */

using System;
using System.Collections.Generic;
using System.IO;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Configuration;

namespace xml_converter
{
    class Program
    {        
        public static void Main(string[] args)
        {
            // Console.WriteLine(GetTagNames("test_files/13316-13317-1.xml"));
            // Console.WriteLine(GetTagNames("test_files/13316-13317-1-edited.xml")); 
            try
            {
                // create directories
                Globals.LOG.AppendHeader(); // init/refresh log directory 
                Globals.LOG.AppendLocation("Start");
                if (Globals.TRANSFER) { Directory.CreateDirectory(Globals.PROCESSED_DIR_TXT); }
                if (Globals.KEEP_XML) { Directory.CreateDirectory(Globals.PROCESSED_DIR_XML); }
                Directory.CreateDirectory(Globals.REGULATION_DIR);

                Globals.CONNECTION.Open(); // open db connection
                
                // record job start time
                Guid g = Guid.NewGuid();
                SqlCommand SqlComm = new SqlCommand("INSERT INTO " + Globals.JOB_TABLE + "(Job_Id, Job_Type, Start_Time) VALUES(@guid, @task, @start)", Globals.CONNECTION);
                SqlComm.Parameters.AddWithValue("@guid", g);
                SqlComm.Parameters.AddWithValue("@task", "Lexis Nexis Parser");
                SqlComm.Parameters.AddWithValue("@start", DateTime.Now.ToString("yyyy-MM-dd h:mm:ss tt"));
                try { SqlComm.ExecuteNonQuery(); } catch (Exception e) { Console.WriteLine(e); }
                Globals.LOG.AppendMessage("Job started");
                
                // get structure
                SqlCommand getDefinitions = new SqlCommand("SELECT * FROM " + Globals.STRUCTURE_TABLE + " WHERE Action=1", Globals.CONNECTION);
                SqlDataAdapter definitions_adapter = new SqlDataAdapter(getDefinitions);
                DataTable definitions = new DataTable();
                definitions_adapter.Fill(definitions);
                Globals.LOG.AppendMessage("Read structure from " + Globals.STRUCTURE_TABLE);
                
                // get column names
                List<string> file_definitions = ConfigurationManager.AppSettings.Get("1").Split(',').Select(p => p.Trim()).ToList();
                List<string> content_definitions = ConfigurationManager.AppSettings.Get("2").Split(',').Select(p => p.Trim()).ToList();
                List<string> user_definitions = ConfigurationManager.AppSettings.Get("3").Split(',').Select(p => p.Trim()).ToList();
                List<string> reg_definitions = ConfigurationManager.AppSettings.Get("4").Split(',').Select(p => p.Trim()).ToList();
                Globals.LOG.AppendMessage("Parsed definitions in " + Globals.STRUCTURE_TABLE);

                // refresh working directory
                if (Directory.Exists(Globals.REGULATION_DIR)) { Directory.Delete(Globals.REGULATION_DIR, true); }
                Directory.CreateDirectory(Globals.REGULATION_DIR);
                
                foreach (DataRow definition in definitions.Rows) // each subscription definition
                {
                    try
                    {
                        // subscription-specific column keys
                        List<string> file_meta = new List<string>();
                        List<string> content_meta = new List<string>();
                        List<string> user_meta = new List<string>();
                        List<string> reg_meta = new List<string>();

                        foreach (string file_definition in file_definitions) { file_meta.Add(definition[file_definition].ToString()); }
                        foreach (string content_definition in content_definitions) { content_meta.Add(definition[content_definition].ToString()); }
                        foreach (string user_definition in user_definitions) { user_meta.Add(definition[user_definition].ToString()); }
                        foreach (string reg_definition in reg_definitions) { reg_meta.Add(definition[reg_definition].ToString()); }

                        string subscription = user_meta[0].ToString();

                        // parse regulation files
                        ReadDir(
                            new Regulator(Globals.REGULATION_DIR, user_meta, file_meta, content_meta, reg_meta),
                            Globals.START_DIR + "/" + subscription,
                            Globals.REGULATION_DIR,
                            ConfigurationManager.AppSettings.Get("Content_Delimiter")
                        );

                        // refresh working directory
                        if (!Globals.KEEP_EXCEPTIONS)
                        {
                            try { Directory.Delete(Globals.REGULATION_DIR, true); }
                            catch (Exception e) { Globals.LOG.AppendException(e, false); Console.WriteLine(e); }
                        }
                    }
                    catch (Exception e) { Globals.LOG.AppendException(e, false); } // catch regulations not listed/found
                }

                // record job end time
                SqlComm = new SqlCommand("UPDATE " + Globals.JOB_TABLE + " SET End_Time=@end WHERE Job_Id=@guid", Globals.CONNECTION);
                SqlComm.Parameters.AddWithValue("@guid", g);
                SqlComm.Parameters.AddWithValue("@end", DateTime.Now.ToString("yyyy-MM-dd h:mm:ss tt"));
                SqlComm.ExecuteNonQuery();
                Globals.LOG.AppendMessage("Job completed");

                Globals.CONNECTION.Close(); // close db connection
            }
            catch (Exception e) { Globals.LOG.AppendError(e); Console.WriteLine(e); } // catch premature termination
           
        }

        private static void MoveDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.EnumerateFiles(source))
            {
                FileInfo ifile = new FileInfo(file);
                if (File.Exists(destination + "/" + ifile.Name))
                {
                    File.Delete(destination + "/" + ifile.Name);
                }
                File.Move(file, destination + "/" + ifile.Name);
                File.Delete(file);
            }
        }

        public static SqlCommand CreateInsertCommand(string table, List<List<string>> relationship_configurations, Regulation regulation, SqlConnection connection)
        {
            string head = "INSERT INTO " + table;
            string body = " (";
            string body_alias = " VALUES(";

            //foreach (List<string> relationship_configuration in relationship_configurations)
            for (int i = 0; i < relationship_configurations.Count; i++)
            {
                List<string> relationship_configuration = relationship_configurations[i];
                body += relationship_configuration[0] + ", ";
                body_alias += "@" + relationship_configuration[0] + ", ";
            }

            body = body.Substring(0, body.Length - 2) + ")";
            body_alias = body_alias.Substring(0, body_alias.Length - 2) + ")";

            string cmdText = head + body + body_alias;

            SqlCommand SqlCmd = new SqlCommand(cmdText, Globals.CONNECTION);

            //foreach (List<string> relationship_configuration in relationship_configurations)
            for (int i = 0; i < relationship_configurations.Count; i++)
            {
                List<string> relationship_configuration = relationship_configurations[i];
                switch (relationship_configuration[1])
                {
                    case "1":
                        SqlCmd.Parameters.AddWithValue("@" + relationship_configuration[0], ApplyFormat(regulation.GetFileItem(Int32.Parse(relationship_configuration[2])), i));
                        break;
                    case "2":
                        SqlCmd.Parameters.AddWithValue("@" + relationship_configuration[0], ApplyFormat(regulation.GetContentItem(Int32.Parse(relationship_configuration[2])), i));
                        break;
                    case "3":
                        SqlCmd.Parameters.AddWithValue("@" + relationship_configuration[0], ApplyFormat(regulation.GetUserItem(Int32.Parse(relationship_configuration[2])), i));
                        break;
                    case "4":
                        SqlCmd.Parameters.AddWithValue("@" + relationship_configuration[0], ApplyFormat(regulation.GetRegulationItem(Int32.Parse(relationship_configuration[2])), i));
                        break;
                }
            }
            return SqlCmd;
        }

        private static string ApplyFormat(string text, int idx)
        {
            switch (Globals.FORMAT_MAP[idx][0])
            {
                case "$after":                              return EverythingAfter(text, Globals.FORMAT_MAP[idx][1]);
                case "$between":                            return EverythingBetween(text, Globals.FORMAT_MAP[idx][1], Globals.FORMAT_MAP[idx][2]);
                case "$soft_title":                         return SoftTitleCase(text);
                case "$hard_title":                         return HardTitleCase(text);
                case "$adjusted_title":                     return AdjustedTitleCase(text);
                case "$lower":                              return LowerCase(text);
                case "$upper":                              return UpperCase(text);
                case "$remove_special":                     return RemoveSpecial(text);
                case "$separate_number":                    return SeparateTitleByNumber(text);
                case "$separate_number+$adjusted_title":    return SeparateTitleByNumber(AdjustedTitleCase(text));
                default:                                    return text;
            }
        }
        public static string EverythingAfter(string text, string instance)
        {
            int ix = text.IndexOf(instance);
            if (ix > -1) { return text.Substring(ix + instance.Length).Trim(); }
            return text;
        }

        public static string EverythingBetween(string text, string start_instance, string end_instance)
        {
            int pFrom = text.IndexOf(start_instance) + start_instance.Length;
            int pTo = text.LastIndexOf(end_instance);
            if (pTo > -1 && pFrom > -1) { return text.Substring(pFrom, pTo - pFrom).Trim(); }
            return text;
        }
        
        public static string SeparateTitleByNumber(string text)
        {
            /* KNOWN CASES:
             * Title 12
             * Title 12.1.1
             * Title 12-a
             * Title 12-a-i
             * Title A2
             * Title A
             */

            try { if (Regex.IsMatch(Regex.Replace(text.Split(' ').Skip(1).FirstOrDefault().Trim(), @"[^0-9a-zA-Z]+", ""), @"^\d+$")) { return text; } } catch (Exception e) { Console.WriteLine(e); return text; }
            int idx = -1;
            string digits = Regex.Match(text, @"(\d+[.-]?)+").ToString();
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsDigit(text.ElementAt(i)))
                {
                    idx = i;
                    break;
                }
            }
            if (idx >= 0)
            {
                int titleLength = text.Substring(0, idx).Trim().Length;
                string title = text.Substring(0, titleLength) + " " + digits + " " + text.Substring(digits.Length + idx);
                return title;
            } return text;
        }

        public static string AdjustedTitleCase(string text) {
            List<string> preps = new List<string>() { "and", "any", "at", "from", "into", "of", "on", "or", "some", "the", "to", "is", "that", "in", "for", };
            List<string> tokens = HardTitleCase(text).Split(' ').ToList();

            string former = tokens[0];
            tokens.RemoveAt(0);

            foreach (string token in tokens) { former += (preps.Contains(token.Trim().ToLower()) ? " " + token.ToLower() : " " + token); }
            return former;
        }
        public static string SoftTitleCase(string text) { return Globals.TI.ToTitleCase(text); }
        public static string HardTitleCase(string text) { return Globals.TI.ToTitleCase(text.ToLower()); }
        public static string LowerCase(string text) { return Globals.TI.ToLower(text.ToLower()); }
        public static string UpperCase(string text) { return Globals.TI.ToUpper(text.ToLower()); }
        public static string RemoveSpecial(string text) { return Regex.Replace(text, "[^a-zA-Z0-9_. ]+", "", RegexOptions.Compiled); }

        public static string GetTagNames(String file)
        {
            string names = "";
            XDocument xdoc = XDocument.Load(file);
            foreach (var name in xdoc.Root.DescendantNodes().OfType<XElement>().Select(x => x.Name).Distinct()) { names += name + "\n"; }
            return names;
        }

        public static IEnumerable<string> SortNumerically(IEnumerable<string> list)
        {
            int maxLen = list.Select(s => s.Length).Max();
            return list.Select(s => new
            {
                OrgStr = s,
                SortStr = Regex.Replace(s, @"(\d+)|(\D+)", m => m.Value.PadLeft(maxLen, char.IsDigit(m.Value[0]) ? ' ' : '\xffff'))
            })
            .OrderBy(x => x.SortStr)
            .Select(x => x.OrgStr);
        }

        public static void ReadDir(Regulator r, string dir, string to, string boundary)
        {
            Globals.LOG.AppendLocation();
            // working subdirectory
            string sub = new DirectoryInfo(dir).Name;
            string sub_path = to + "/" + sub;
            string[] sub_paths = SortNumerically(Directory.GetFiles(dir, "*.txt", SearchOption.AllDirectories)).ToArray();
                       
            if (sub_paths.Length > 0)
            {
                Directory.CreateDirectory(sub_path);

                Console.WriteLine("subscription = " + sub);
                Globals.LOG.AppendMessage("Begin read for subscription = " + sub);

                // parse regulation file then purge from working directory
                foreach (string file in sub_paths)
                {
                    ReadFile(file, sub_path, boundary); // read
                    if (!r.ParseReg()) { return; } // parse + purge
                }
            }
        }

        private static void ReadFile(string file, string to, string boundary)
        {
            Globals.LOG.AppendLocation();
            int count = 0;
            StringBuilder sb = new StringBuilder();
            StreamWriter dest = null;

            //var instances = Regex.Matches(File.ReadAllText(file), Regex.Escape(boundary), RegexOptions.IgnoreCase).Count;
            //Console.WriteLine("# OF BOUNDS = " + instances);
            // if (instances > 100) { /* limit or buffer */ }

            if (Directory.GetFiles(to, (Path.GetFileNameWithoutExtension(file) + "-1.xml")).Length > 0) { Globals.LOG.AppendMessage(file + " was already read. Attempting parse again."); return; }

            // lazy scan
            foreach (string line in File.ReadLines(file))
            {
                if (line.Contains(boundary) && sb.Length > 0)
                {
                    // build
                    count++;
                    string content = sb.ToString();
                    sb.Clear();

                    // clean
                    string destination = Path.GetFileNameWithoutExtension(file) + "-" + count.ToString();
                    int positionOfXML = content.IndexOf("<?xml");
                    if (positionOfXML >= 0)
                    {
                        string description = content.Substring(positionOfXML);

                        // write
                        dest = new StreamWriter(to + "/" + destination + ".xml");
                        dest.WriteLine(description);
                        dest.Close();
                    }
                    else { Globals.LOG.AppendMessage(Path.GetFileName(to) + "/" + destination + " is an empty content file."); }
                }
                else { sb.Append(line); }
            } 
        }
    }
}