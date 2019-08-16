/* Copyright (c) 2018 Rebecca Ramnauth */

using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using xml_converter;

class Logger
{
    readonly string p;
    readonly int level;

    public Logger(string path)
    {
        RefreshLogs();

        p = path + "/lexis_xml_parser_" + DateTime.Now.ToString("MMddyyyy") + ".txt";
        level = AppSettings.Get<int>("LogLevel");

        FileInfo file = new FileInfo(path);
        file.Directory.Create(); // if exists, ignores
        FileStream fs = new FileStream(p, FileMode.OpenOrCreate);
        fs.Close();
    }

    private void RefreshLogs()
    {
        Directory.GetFiles(ConfigurationManager.AppSettings.Get("LogDir"))
                 .Select(f => new FileInfo(f))
                 .Where(f => f.LastWriteTime < DateTime.Now.AddDays(-1 * AppSettings.Get<double>("LogRetention")))
                 .ToList()
                 .ForEach(f => f.Delete());
    }
    
    public void AppendException(Exception message, bool ovrride, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (level >= 2 || ovrride)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("DEBUG@" + DateTime.Now.ToShortDateString() + " "
                + DateTime.Now.ToLongTimeString() + " "
                + Path.GetFileName(sourceFilePath)
                + "/" + memberName
                + "/line:" + sourceLineNumber + ": "
                + "[SUCCESSFULLY CAUGHT] " + message.GetType().ToString()
            );
            if (level != 2) { sb.AppendLine("[DETAILS] " + message.ToString() + "\n"); }

            using (StreamWriter sw = File.AppendText(p))
            {
                sw.Write(sb.ToString());
                //sw.Close();
            }
        }
    }

    public void AppendError(Exception message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine( "DEBUG@" + DateTime.Now.ToShortDateString() + " "
            + DateTime.Now.ToLongTimeString() + " "
            + Path.GetFileName(sourceFilePath)
            + "/" + memberName
            + "/line:" + sourceLineNumber + ": "
            + "[WARNING] " + message.ToString()
        );

        using (StreamWriter sw = File.AppendText(p))
        {
            sw.Write(sb.ToString());
            //sw.Close();
        }
    }

    public void AppendMessage(string message, [CallerFilePath] string sourceFilePath = "")
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("DEBUG@" + DateTime.Now.ToShortDateString() + " "
            + DateTime.Now.ToLongTimeString() + " "
            + Path.GetFileName(sourceFilePath) + ": "
            + "[MESSAGE] " + message.ToString()
        );

        using (StreamWriter sw = File.AppendText(p))
        {
            sw.Write(sb.ToString());
            //sw.Close();
        }
    }

    public void AppendLocation (string message = "", [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (level >= 4)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("DEBUG@" + DateTime.Now.ToShortDateString() + " "
                + DateTime.Now.ToLongTimeString() + " "
                + Path.GetFileName(sourceFilePath)
                + "/" + memberName
                + "/line:" + sourceLineNumber + ": "
                + "[LOCATION] " + message
            );

            using (StreamWriter sw = File.AppendText(p))
            {
                sw.Write(sb.ToString());
                //sw.Close();
            }
        }
    }

    public void AppendHeader()
    {
        using (StreamWriter sw = File.AppendText(p))
        {
            sw.WriteLine("--------------------------------------------------------------------------");
            sw.WriteLine("           PARSING [LOG LEVEL " + level + "] , on " + DateTime.Now.ToShortDateString() + " at " + DateTime.Now.ToLongTimeString());
            sw.WriteLine("--------------------------------------------------------------------------");
            //sw.Close();
        }
    }

    public void DumpLog() {
        StreamReader r = File.OpenText(p);
        string line;
        while ((line = r.ReadLine()) != null) {
            Console.WriteLine(line);
        }
        r.Close();
    } 
}