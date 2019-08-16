/* Copyright (c) 2018 Rebecca Ramnauth */

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using xml_converter;

class Globals
{
    // SQL DATA CLIENT
    public static readonly SqlConnection CONNECTION = new SqlConnection(ConfigurationManager.ConnectionStrings["ConnectionKey"].ConnectionString);
    public static readonly String JOB_TABLE = ConfigurationManager.AppSettings.Get("JobLog");
    public static readonly String FILE_TABLE = ConfigurationManager.AppSettings.Get("FileLog");
    public static readonly String CONTENT_TABLE = ConfigurationManager.AppSettings.Get("ContentLog");
    public static readonly String STRUCTURE_TABLE = ConfigurationManager.AppSettings.Get("Reg_Structure");

    // DIRECTORY CONTROLS
    public static readonly bool TRANSFER = AppSettings.Get<bool>("TransferProcessed");
    public static readonly bool KEEP_XML = AppSettings.Get<bool>("KeepXMLContent");
    public static readonly bool KEEP_EXCEPTIONS = AppSettings.Get<bool>("KeepExceptionFiles");

    // DIRECTORIES
    public static readonly String PROCESSED_DIR_TXT = ConfigurationManager.AppSettings.Get("ProcessedTXTDir");
    public static readonly String PROCESSED_DIR_XML = ConfigurationManager.AppSettings.Get("ProcessedXMLDir");
    public static readonly String START_DIR = ConfigurationManager.AppSettings.Get("InflowDir");
    public static readonly String REGULATION_DIR = ConfigurationManager.AppSettings.Get("WorkingDir");

    // RELATIONSHIPS
    public static List<List<string>> RELATIONSHIP_MAP = ConfigurationManager.AppSettings
        .Get("Structure-Log-Relationship").Split(';')
        .Select(line => line.Split(',')
        .Select(item => item.Trim()).ToList()).ToList();
    public static List<List<string>> FORMAT_MAP = RELATIONSHIP_MAP
        .Select(line => line.ElementAt(line.Count - 1).Trim().Split(' ')
        .Select(item => Program.EverythingBetween(item.Trim(), "'", "'")).ToList()).ToList();
    private static readonly string[] delete_id = ConfigurationManager.AppSettings.Get("Delete_Identifier").Split(',');
    public static string DELETE_VAL = delete_id[1].Trim();
    public static int DELETE_IDX = Int32.Parse(delete_id[0]);
    public static int DELETE_FIL = AppSettings.Get<int>("Reg_Delete_Filter");
    public static TextInfo TI = new CultureInfo("en-US", false).TextInfo;

    // LOG MANAGEMENT
    public static Logger LOG = new Logger(ConfigurationManager.AppSettings.Get("LogDir"));
}