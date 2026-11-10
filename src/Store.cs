// SPDX-License-Identifier: GPL-3.0-only
//
// Store.cs - reading and writing profiles on disk.
//
// Layout of the data folder (default: %LOCALAPPDATA%\EchoHUD):
//
//   profiles\<name>.xml   one file per profile, plain XML, safe to back up
//   last-profile.txt      name of the profile to open next time
//
// Set the ECHOHUD_DATA_DIR environment variable to use another folder
// (the tests do this so they never touch real profiles).
//
// Nothing here is sent anywhere: the program has no network code at all.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace EchoHud
{
    internal static class Store
    {
        const int MaxNameLength = 40;
        const long MaxFileBytes = 1024 * 1024;

        internal static string DataRoot
        {
            get
            {
                string custom = Environment.GetEnvironmentVariable("ECHOHUD_DATA_DIR");
                if (!String.IsNullOrEmpty(custom)) return Path.GetFullPath(custom);
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EchoHUD");
            }
        }

        static string ProfilesFolder { get { return Path.Combine(DataRoot, "profiles"); } }
        static string LastProfileFile { get { return Path.Combine(DataRoot, "last-profile.txt"); } }

        /// <summary>
        /// Profile names become file names, so only a conservative character
        /// set is allowed. This also rules out path tricks such as "..\x".
        /// </summary>
        internal static bool IsValidName(string name)
        {
            if (String.IsNullOrEmpty(name) || name.Length > MaxNameLength) return false;
            if (name != name.Trim()) return false;
            foreach (char c in name)
                if (!(Char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_')) return false;
            foreach (string reserved in new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "LPT1", "LPT2", "LPT3" })
                if (String.Equals(name, reserved, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        static string PathFor(string name)
        {
            if (!IsValidName(name)) throw new InvalidDataException("Profile name is not valid.");
            return Path.Combine(ProfilesFolder, name + ".xml");
        }

        /// <summary>Names of all saved profiles, alphabetically.</summary>
        internal static List<string> ListProfiles()
        {
            List<string> names = new List<string>();
            if (Directory.Exists(ProfilesFolder))
                foreach (string file in Directory.GetFiles(ProfilesFolder, "*.xml"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (IsValidName(name)) names.Add(name);
                }
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return names;
        }

        internal static bool Exists(string name)
        {
            return IsValidName(name) && File.Exists(PathFor(name));
        }

        /// <summary>
        /// Saves a profile without ever leaving a half-written file behind:
        /// the new content goes to a temporary file first and is swapped in
        /// only once it is completely on disk.
        /// </summary>
        internal static void Save(Profile profile)
        {
            profile.Validate();
            string target = PathFor(profile.Name);
            Directory.CreateDirectory(ProfilesFolder);
            string temporary = target + ".tmp";
            File.WriteAllText(temporary, Serialize(profile), new UTF8Encoding(false));
            if (File.Exists(target)) File.Replace(temporary, target, null);
            else File.Move(temporary, target);
        }

        /// <summary>Loads and validates a profile. Throws if the file is missing or damaged.</summary>
        internal static Profile Load(string name)
        {
            string path = PathFor(name);
            if (new FileInfo(path).Length > MaxFileBytes) throw new InvalidDataException("Profile file is too large.");
            Profile profile = Parse(File.ReadAllText(path, Encoding.UTF8));
            profile.Name = name; // The file name is the source of truth.
            return profile;
        }

        internal static void Delete(string name)
        {
            string path = PathFor(name);
            if (File.Exists(path)) File.Delete(path);
        }

        internal static string Serialize(Profile profile)
        {
            XmlWriterSettings settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
            using (MemoryStream stream = new MemoryStream())
            {
                using (XmlWriter writer = XmlWriter.Create(stream, settings))
                    new XmlSerializer(typeof(Profile)).Serialize(writer, profile);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>
        /// Profiles may be shared between players, so the file is treated as
        /// untrusted input: no DTDs, no external references, and every value
        /// is range-checked before it is used.
        /// </summary>
        internal static Profile Parse(string xml)
        {
            XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            try
            {
                using (XmlReader reader = XmlReader.Create(new StringReader(xml), settings))
                {
                    Profile profile = (Profile)new XmlSerializer(typeof(Profile)).Deserialize(reader);
                    if (profile == null) throw new InvalidDataException("Profile file is empty.");
                    profile.Validate();
                    return profile;
                }
            }
            catch (InvalidOperationException error)
            {
                // XmlSerializer wraps malformed-XML errors in InvalidOperationException.
                throw new InvalidDataException("Profile file could not be read.", error);
            }
            catch (XmlException error)
            {
                throw new InvalidDataException("Profile file could not be read.", error);
            }
        }

        /// <summary>Name of the profile used last time, or null on first run.</summary>
        internal static string LastProfile
        {
            get
            {
                try
                {
                    if (!File.Exists(LastProfileFile)) return null;
                    string name = File.ReadAllText(LastProfileFile).Trim();
                    return IsValidName(name) ? name : null;
                }
                catch (IOException) { return null; }
                catch (UnauthorizedAccessException) { return null; }
            }
            set
            {
                try
                {
                    Directory.CreateDirectory(DataRoot);
                    File.WriteAllText(LastProfileFile, value ?? "");
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
