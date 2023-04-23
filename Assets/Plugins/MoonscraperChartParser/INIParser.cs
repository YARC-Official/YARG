/*******************************
Version: 1.0
Project Boon
*******************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public class INIParser
{
    #region "Declarations"

    // *** Error: In case there're errors, this will changed to some value other than 1 ***
    // Error codes: 
    // 1: Null TextAsset
    public int error = 0;

    // *** Lock for thread-safe access to file and local cache ***
    private object m_Lock = new object();

    // *** File name ***
    private string m_FileName = null;
    public string FileName
    {
        get
        {
            return m_FileName;
        }
    }

    // ** String represent Ini
    private string m_iniString = null;
    public string iniString
    {
        get
        {
            return m_iniString;
        }
    }

    // *** Automatic flushing flag ***
    private bool m_AutoFlush = false;

    // *** Local cache ***
    private Dictionary<string, Dictionary<string, string>> m_Sections = new Dictionary<string, Dictionary<string, string>>();
    private Dictionary<string, Dictionary<string, string>> m_Modified = new Dictionary<string, Dictionary<string, string>>();

    // *** Local cache modified flag ***
    private bool m_CacheModified = false;

    #endregion

    #region "Methods"

    // *** Open ini file by path ***
    public void Open(string path)
    {
        m_FileName = path;

        if (File.Exists(m_FileName))
        {
            m_iniString = File.ReadAllText(m_FileName);
        }
        else
        {
            //If file does not exist, create one
            var temp = File.Create(m_FileName);
            temp.Close();
            m_iniString = "";
        }

        Initialize(m_iniString, false);
    }

    // *** Open ini file by TextAsset: All changes is saved to local storage ***
    public void Open(TextAsset name)
    {
        if (name == null)
        {
            // In case null asset, treat as opened an empty file
            error = 1;
            m_iniString = "";
            m_FileName = null;
            Initialize(m_iniString, false);
        }
        else
        {
            m_FileName = Application.persistentDataPath + name.name;

            //*** Find the TextAsset in the local storage first ***
            if (File.Exists(m_FileName))
            {
                m_iniString = File.ReadAllText(m_FileName);
            }
            else m_iniString = name.text;
            Initialize(m_iniString, false);
        }
    }

    // *** Open ini file from string ***
    public void OpenFromString(string str)
    {
        m_FileName = null;
        Initialize(str, false);
    }

    // *** Get the string content of ini file ***
    public override string ToString()
    {
        return m_iniString;
    }

    private void Initialize(string iniString, bool AutoFlush)
    {
        m_iniString = iniString;
        m_AutoFlush = AutoFlush;
        Refresh();
    }

    // *** Close, save all changes to ini file ***
    public void Close()
    {
        lock (m_Lock)
        {
            PerformFlush();

            //Clean up memory
            m_FileName = null;
            m_iniString = null;
        }
    }

    // *** Parse section name ***
    private string ParseSectionName(string Line)
    {
        if (!Line.StartsWith("[")) return null;
        if (!Line.EndsWith("]")) return null;
        if (Line.Length < 3) return null;
        return Line.Substring(1, Line.Length - 2);
    }

    // *** Parse key+value pair ***
    private bool ParseKeyValuePair(string Line, ref string Key, ref string Value)
    {
        // *** Check for key+value pair ***
        int i;
        if ((i = Line.IndexOf('=')) <= 0) return false;

        int j = Line.Length - i - 1;
        Key = Line.Substring(0, i).Trim();
        if (Key.Length <= 0) return false;

        Value = (j > 0) ? (Line.Substring(i + 1, j).Trim()) : ("");
        return true;
    }

    // *** If a line is neither SectionName nor key+value pair, it's a comment ***
    private bool isComment(string Line)
    {
        string tmpKey = null, tmpValue = null;
        if (ParseSectionName(Line) != null) return false;
        if (ParseKeyValuePair(Line, ref tmpKey, ref tmpValue)) return false;
        return true;
    }

    // *** Read file contents into local cache ***
    private void Refresh()
    {
        lock (m_Lock)
        {
            StringReader sr = null;
            try
            {
                // *** Clear local cache ***
                m_Sections.Clear();
                m_Modified.Clear();

                // *** String Reader ***
                sr = new StringReader(m_iniString);


                // *** Read up the file content ***
                Dictionary<string, string> CurrentSection = null;
                string s;
                string SectionName;
                string Key = null;
                string Value = null;
                while ((s = sr.ReadLine()) != null)
                {
                    s = s.Trim();

                    // *** Check for section names ***
                    SectionName = ParseSectionName(s);
                    if (SectionName != null)
                    {
                        // *** Only first occurrence of a section is loaded ***
                        if (m_Sections.ContainsKey(SectionName))
                        {
                            CurrentSection = null;
                        }
                        else
                        {
                            CurrentSection = new Dictionary<string, string>();
                            m_Sections.Add(SectionName, CurrentSection);
                        }
                    }
                    else if (CurrentSection != null)
                    {
                        // *** Check for key+value pair ***
                        if (ParseKeyValuePair(s, ref Key, ref Value))
                        {
                            // *** Only first occurrence of a key is loaded ***
                            if (!CurrentSection.ContainsKey(Key))
                            {
                                CurrentSection.Add(Key, Value);
                            }
                        }
                    }
                }
            }
            finally
            {
                // *** Cleanup: close file ***
                if (sr != null) sr.Close();
                sr = null;
            }
        }
    }

    private void PerformFlush()
    {
        // *** If local cache was not modified, exit ***
        if (!m_CacheModified) return;
        m_CacheModified = false;

        // *** Copy content of original iniString to temporary string, replace modified values ***
        StringWriter sw = new StringWriter();

        try
        {
            Dictionary<string, string> CurrentSection = null;
            Dictionary<string, string> CurrentSection2 = null;
            StringReader sr = null;
            try
            {
                // *** Open the original file ***
                sr = new StringReader(m_iniString);

                // *** Read the file original content, replace changes with local cache values ***
                string s;
                string SectionName;
                string Key = null;
                string Value = null;
                bool Unmodified;
                bool Reading = true;

                bool Deleted = false;
                string Key2 = null;
                string Value2 = null;

                StringBuilder sb_temp;

                while (Reading)
                {
                    s = sr.ReadLine();
                    Reading = (s != null);

                    // *** Check for end of iniString ***
                    if (Reading)
                    {
                        Unmodified = true;
                        s = s.Trim();
                        SectionName = ParseSectionName(s);
                    }
                    else
                    {
                        Unmodified = false;
                        SectionName = null;
                    }

                    // *** Check for section names ***
                    if ((SectionName != null) || (!Reading))
                    {
                        if (CurrentSection != null)
                        {
                            // *** Write all remaining modified values before leaving a section ****
                            if (CurrentSection.Count > 0)
                            {
                                // *** Optional: All blank lines before new values and sections are removed ****
                                sb_temp = sw.GetStringBuilder();
                                while ((sb_temp[sb_temp.Length - 1] == '\n') || (sb_temp[sb_temp.Length - 1] == '\r'))
                                {
                                    sb_temp.Length = sb_temp.Length - 1;
                                }
                                sw.WriteLine();

                                foreach (string fkey in CurrentSection.Keys)
                                {
                                    if (CurrentSection.TryGetValue(fkey, out Value))
                                    {
                                        sw.Write(fkey);
                                        sw.Write('=');
                                        sw.WriteLine(Value);
                                    }
                                }
                                sw.WriteLine();
                                CurrentSection.Clear();
                            }
                        }

                        if (Reading)
                        {
                            // *** Check if current section is in local modified cache ***
                            if (!m_Modified.TryGetValue(SectionName, out CurrentSection))
                            {
                                CurrentSection = null;
                            }
                        }
                    }
                    else if (CurrentSection != null)
                    {
                        // *** Check for key+value pair ***
                        if (ParseKeyValuePair(s, ref Key, ref Value))
                        {
                            if (CurrentSection.TryGetValue(Key, out Value))
                            {
                                // *** Write modified value to temporary file ***
                                Unmodified = false;
                                CurrentSection.Remove(Key);

                                sw.Write(Key);
                                sw.Write('=');
                                sw.WriteLine(Value);
                            }
                        }
                    }

                    // ** Check if the section/key in current line has been deleted ***
                    if (Unmodified)
                    {
                        if (SectionName != null)
                        {
                            if (!m_Sections.ContainsKey(SectionName))
                            {
                                Deleted = true;
                                CurrentSection2 = null;
                            }
                            else
                            {
                                Deleted = false;
                                m_Sections.TryGetValue(SectionName, out CurrentSection2);
                            }

                        }
                        else if (CurrentSection2 != null)
                        {
                            if (ParseKeyValuePair(s, ref Key2, ref Value2))
                            {
                                if (!CurrentSection2.ContainsKey(Key2)) Deleted = true;
                                else Deleted = false;
                            }
                        }
                    }


                    // *** Write unmodified lines from the original iniString ***
                    if (Unmodified)
                    {
                        if (isComment(s)) sw.WriteLine(s);
                        else if (!Deleted) sw.WriteLine(s);
                    }
                }

                // *** Close string reader ***
                sr.Close();
                sr = null;
            }
            finally
            {
                // *** Cleanup: close string reader ***                  
                if (sr != null) sr.Close();
                sr = null;
            }

            // *** Cycle on all remaining modified values ***
            foreach (KeyValuePair<string, Dictionary<string, string>> SectionPair in m_Modified)
            {
                CurrentSection = SectionPair.Value;
                if (CurrentSection.Count > 0)
                {
                    if (!string.IsNullOrEmpty(sw.ToString()))
                        sw.WriteLine();

                    // *** Write the section name ***
                    sw.Write('[');
                    sw.Write(SectionPair.Key);
                    sw.WriteLine(']');

                    // *** Cycle on all key+value pairs in the section ***
                    foreach (KeyValuePair<string, string> ValuePair in CurrentSection)
                    {
                        // *** Write the key+value pair ***
                        sw.Write(ValuePair.Key);
                        sw.Write('=');
                        sw.WriteLine(ValuePair.Value);
                    }
                    CurrentSection.Clear();
                }
            }
            m_Modified.Clear();

            // *** Get result to iniString ***
            m_iniString = sw.ToString();
            sw.Close();
            sw = null;

            // ** Write iniString to file ***
            if (m_FileName != null)
            {
                File.WriteAllText(m_FileName, m_iniString);
            }
        }
        finally
        {
            // *** Cleanup: close string writer ***                  
            if (sw != null) sw.Close();
            sw = null;
        }
    }

    // *** Check if the section exists ***
    public bool IsSectionExists(string SectionName)
    {
        return m_Sections.ContainsKey(SectionName);
    }

    // *** Check if the key exists ***
    public bool IsKeyExists(string SectionName, string Key)
    {
        Dictionary<string, string> Section;

        // *** Check if the section exists ***
        if (m_Sections.ContainsKey(SectionName))
        {
            m_Sections.TryGetValue(SectionName, out Section);

            // If the key exists
            return Section.ContainsKey(Key);
        }
        else return false;
    }

    // *** Delete a section in local cache ***
    public void SectionDelete(string SectionName)
    {
        // *** Delete section if exists ***
        if (IsSectionExists(SectionName))
        {
            lock (m_Lock)
            {
                m_CacheModified = true;
                m_Sections.Remove(SectionName);

                //Also delete in modified cache if exist
                m_Modified.Remove(SectionName);

                // *** Automatic flushing : immediately write any modification to the file ***
                if (m_AutoFlush) PerformFlush();
            }
        }
    }

    // *** Delete a key in local cache ***
    public void KeyDelete(string SectionName, string Key)
    {
        Dictionary<string, string> Section;

        //Delete key if exists
        if (IsKeyExists(SectionName, Key))
        {
            lock (m_Lock)
            {
                m_CacheModified = true;
                m_Sections.TryGetValue(SectionName, out Section);
                Section.Remove(Key);

                //Also delete in modified cache if exist
                if (m_Modified.TryGetValue(SectionName, out Section)) Section.Remove(SectionName);

                // *** Automatic flushing : immediately write any modification to the file ***
                if (m_AutoFlush) PerformFlush();
            }
        }

    }

    // *** Read a value from local cache ***
    public string ReadValue(string SectionName, string Key, string DefaultValue)
    {
        lock (m_Lock)
        {
            // *** Check if the section exists ***
            Dictionary<string, string> Section;
            if (!m_Sections.TryGetValue(SectionName, out Section)) return DefaultValue;

            // *** Check if the key exists ***
            string Value;
            if (!Section.TryGetValue(Key, out Value)) return DefaultValue;

            // *** Return the found value ***
            return Value;
        }
    }

    // *** Insert or modify a value in local cache ***
    public void WriteValue(string SectionName, string Key, string Value)
    {
        lock (m_Lock)
        {
            // *** Flag local cache modification ***
            m_CacheModified = true;

            // *** Check if the section exists ***
            Dictionary<string, string> Section;
            if (!m_Sections.TryGetValue(SectionName, out Section))
            {
                // *** If it doesn't, add it ***
                Section = new Dictionary<string, string>();
                m_Sections.Add(SectionName, Section);
            }

            // *** Modify the value ***
            if (Section.ContainsKey(Key)) Section.Remove(Key);
            Section.Add(Key, Value);

            // *** Add the modified value to local modified values cache ***
            if (!m_Modified.TryGetValue(SectionName, out Section))
            {
                Section = new Dictionary<string, string>();
                m_Modified.Add(SectionName, Section);
            }

            if (Section.ContainsKey(Key)) Section.Remove(Key);
            Section.Add(Key, Value);

            // *** Automatic flushing : immediately write any modification to the file ***
            if (m_AutoFlush) PerformFlush();
        }
    }

    // *** Encode byte array ***
    private string EncodeByteArray(byte[] Value)
    {
        if (Value == null) return null;

        StringBuilder sb = new StringBuilder();
        foreach (byte b in Value)
        {
            string hex = Convert.ToString(b, 16);
            int l = hex.Length;
            if (l > 2)
            {
                sb.Append(hex.Substring(l - 2, 2));
            }
            else
            {
                if (l < 2) sb.Append("0");
                sb.Append(hex);
            }
        }
        return sb.ToString();
    }

    // *** Decode byte array ***
    private byte[] DecodeByteArray(string Value)
    {
        if (Value == null) return null;

        int l = Value.Length;
        if (l < 2) return new byte[] { };

        l /= 2;
        byte[] Result = new byte[l];
        for (int i = 0; i < l; i++) Result[i] = Convert.ToByte(Value.Substring(i * 2, 2), 16);
        return Result;
    }

    // *** Getters for various types ***
    public bool ReadValue(string SectionName, string Key, bool DefaultValue)
    {
        string StringValue = ReadValue(SectionName, Key, DefaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        int Value;
        if (int.TryParse(StringValue, out Value)) return (Value != 0);
        return DefaultValue;
    }

    public int ReadValue(string SectionName, string Key, int DefaultValue)
    {
        string StringValue = ReadValue(SectionName, Key, DefaultValue.ToString(CultureInfo.InvariantCulture));
        int Value;
        if (int.TryParse(StringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out Value)) return Value;
        return DefaultValue;
    }

    public long ReadValue(string SectionName, string Key, long DefaultValue)
    {
        string StringValue = ReadValue(SectionName, Key, DefaultValue.ToString(CultureInfo.InvariantCulture));
        long Value;
        if (long.TryParse(StringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out Value)) return Value;
        return DefaultValue;
    }

    public double ReadValue(string SectionName, string Key, double DefaultValue)
    {
        string StringValue = ReadValue(SectionName, Key, DefaultValue.ToString(CultureInfo.InvariantCulture));
        double Value;
        if (double.TryParse(StringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out Value)) return Value;
        return DefaultValue;
    }

    public byte[] ReadValue(string SectionName, string Key, byte[] DefaultValue)
    {
        string StringValue = ReadValue(SectionName, Key, EncodeByteArray(DefaultValue));
        try
        {
            return DecodeByteArray(StringValue);
        }
        catch (FormatException)
        {
            return DefaultValue;
        }
    }

    public DateTime ReadValue(string SectionName, string Key, DateTime DefaultValue)
    {
        string StringValue = ReadValue(SectionName, Key, DefaultValue.ToString(CultureInfo.InvariantCulture));
        DateTime Value;
        if (DateTime.TryParse(StringValue, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault | DateTimeStyles.AssumeLocal, out Value)) return Value;
        return DefaultValue;
    }

    // *** Setters for various types ***
    public void WriteValue(string SectionName, string Key, bool Value)
    {
        WriteValue(SectionName, Key, (Value) ? ("1") : ("0"));
    }

    public void WriteValue(string SectionName, string Key, int Value)
    {
        WriteValue(SectionName, Key, Value.ToString(CultureInfo.InvariantCulture));
    }

    public void WriteValue(string SectionName, string Key, long Value)
    {
        WriteValue(SectionName, Key, Value.ToString(CultureInfo.InvariantCulture));
    }

    public void WriteValue(string SectionName, string Key, double Value)
    {
        WriteValue(SectionName, Key, Value.ToString(CultureInfo.InvariantCulture));
    }

    public void WriteValue(string SectionName, string Key, byte[] Value)
    {
        WriteValue(SectionName, Key, EncodeByteArray(Value));
    }

    public void WriteValue(string SectionName, string Key, DateTime Value)
    {
        WriteValue(SectionName, Key, Value.ToString(CultureInfo.InvariantCulture));
    }

    /******* Custom stuff *************/

    public enum Formatting
    {
        Default,        // Leaves strings as is
        Whitespaced,    // Adds whitespace after keys and before values if there aren't already
    }

    public string GetSectionValues(string[] SectionNames, Formatting formatting = Formatting.Default)
    {
        StringBuilder sb = new StringBuilder();

        foreach (var SectionName in SectionNames)
        {
            Dictionary<string, string> Section;
            if (!m_Sections.TryGetValue(SectionName, out Section))
            {
                continue;
            }

            foreach (var keyVal in Section)
            {
                if (keyVal.Key.Trim() != string.Empty)
                {
                    switch (formatting)
                    {
                        case Formatting.Whitespaced:
                            {
                                sb.Append(keyVal.Key.Trim());
                                sb.Append(" = ");
                                sb.AppendLine(keyVal.Value.Trim());
                            }
                            break;
                        case Formatting.Default:
                        default:
                            {
                                sb.Append(keyVal.Key);
                                sb.Append('=');
                                sb.AppendLine(keyVal.Value);
                            }
                            break;
                    }
                    
                }
            }
        }

        return sb.ToString();
    }

    public bool IsEmpty
    {
        get
        {
            foreach (var keyVal in m_Sections)
            {
                if (keyVal.Value.Count > 0)
                {
                    return false;
                }
            }

            return true;
        }
    }

    // Copies all data from the ini passed into this
    public void WriteValue(INIParser that)
    {
        // Clear all previous data
        List<string> sectionsToDelete = new List<string>();
        foreach (var Section in m_Sections)
        {
            sectionsToDelete.Add(Section.Key);
        }

        foreach (var Section in sectionsToDelete)
        {
            SectionDelete(Section);
        }

        foreach (var Section in that.m_Sections)
        {
            foreach (var SectionVal in Section.Value)
            {
                WriteValue(Section.Key, SectionVal.Key, SectionVal.Value);
            }
        }
    }

    #endregion
}


