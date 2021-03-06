﻿using Menu;
using RWCustom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using UnityEngine;

namespace Rain_World_Drought.Resource
{
    public static class TextManager
    {
        public static void Patch()
        {
            On.GhostConversation.AddEvents += new On.GhostConversation.hook_AddEvents(TextManager.GhostEvents);
            On.Menu.CreditsTextAndImage.ctor += new On.Menu.CreditsTextAndImage.hook_ctor(CreditReplace);
            On.Menu.OptionsMenu.ShutDownProcess += new On.Menu.OptionsMenu.hook_ShutDownProcess(UpdateLanguage);
        }

        #region LanguageHandler

        public static LanguageID curLang = LanguageID.NULL, lastLang = LanguageID.NULL;

        private static Dictionary<string, LanguageID> CultureToID;
        public static Dictionary<int, LanguageID> OptionToID;

        public static void IDDictInit()
        {
            CultureToID = new Dictionary<string, LanguageID>()
            {
                { "en-US", TextManager.LanguageID.English },
                { "fr-FR", TextManager.LanguageID.French },
                { "it-IT", TextManager.LanguageID.Italian },
                { "de-DE", TextManager.LanguageID.German },
                { "es-ES", TextManager.LanguageID.Spanish },
                { "pt-PT", TextManager.LanguageID.Portuguese },
                { "ja-JP", TextManager.LanguageID.Japanese },
                { "ko-KR", TextManager.LanguageID.Korean },
                { "ru-RU", TextManager.LanguageID.Russian }
            };
            OptionToID = new Dictionary<int, TextManager.LanguageID>()
            {
                { 0 , TextManager.LanguageID.English },
                { 1 , TextManager.LanguageID.French },
                { 2 , TextManager.LanguageID.Italian },
                { 3 , TextManager.LanguageID.German },
                { 4 , TextManager.LanguageID.Spanish },
                { 5 , TextManager.LanguageID.Portuguese },
                { 6 , TextManager.LanguageID.Japanese },
                { 7 , TextManager.LanguageID.Korean }
            };
        }

        public enum LanguageID
        {
            English, French, Italian, German, Spanish, Portuguese, Japanese, Korean, Russian, NULL = -1
        }

        /// <summary>
        /// Converts <see cref="LanguageID"/> to three lettered ID that's used for naming files
        /// </summary>
        private static string LangShort(LanguageID id) => id.ToString().Substring(0, 3);

        private static void UpdateLanguage(On.Menu.OptionsMenu.orig_ShutDownProcess orig, OptionsMenu self)
        {
            orig.Invoke(self);
            // Use ConfigMachine curLang if possible
            if (DroughtMod.hasConfigMachine) { GetLangFromConfigMachine(); }
            else
            {
                if (OptionToID.TryGetValue((int)self.CurrLang, out LanguageID newID)) { curLang = newID; }
                else { curLang = LanguageID.English; }
            }
            //Debug.Log($"Drought uses Language {curLang}");
        }

        public static void GetLangFromConfigMachine()
        {
            CultureInfo ci = DroughtConfigGenerator.GetCultureInfo();
            if (CultureToID.TryGetValue(ci.Name, out LanguageID newID)) { curLang = newID; }
            else { curLang = LanguageID.English; }
        }

        #endregion LanguageHandler

        private static void CreditReplace(On.Menu.CreditsTextAndImage.orig_ctor orig, CreditsTextAndImage self,
           Menu.Menu menu, MenuObject owner, EndCredits.Stage stage)
        {
            orig.Invoke(self, menu, owner, stage);
            if (stage == EndCredits.Stage.VideoCult)
            {
                string path = string.Concat(ResourceManager.assetDir,
                    "Text",
                    Path.DirectorySeparatorChar,
                    "Credits",
                    Path.DirectorySeparatorChar,
                    "01 - VIDEOCULT.txt"
                    );
                if (File.Exists(path))
                {
                    string[] array = File.ReadAllLines(path, Encoding.Default);
                    // Remove old Credits
                    for (int j = self.subObjects.Count - 1; j >= 0; j--)
                    {
                        if (self.subObjects[j] is MenuLabel) { self.subObjects[j].RemoveSprites(); self.RemoveSubObject(self.subObjects[j]); }
                    }
                    // Add new one
                    for (int i = 0; i < array.Length; i++)
                    {
                        array[i] = array[i].Replace("<LINE>", Environment.NewLine);
                        self.subObjects.Add(new MenuLabel(menu, self, array[i], new Vector2(433f, (float)(-(float)i) * 40f), new Vector2(500f, 30f), false));
                        (self.subObjects[self.subObjects.Count - 1] as MenuLabel).label.alignment = FLabelAlignment.Center;
                        (self.subObjects[self.subObjects.Count - 1] as MenuLabel).label.x = -1000f;
                    }
                }
            }
        }

        private static void GhostEvents(On.GhostConversation.orig_AddEvents orig, GhostConversation self)
        {
            switch (self.id)
            {
                case Conversation.ID.Ghost_CC:
                    LoadEventsFromFile(self, EventID.Ghost_CC);
                    break;
                case Conversation.ID.Ghost_SI:
                    LoadEventsFromFile(self, EventID.Ghost_SI);
                    break;
                case Conversation.ID.Ghost_LF:
                    LoadEventsFromFile(self, EventID.Ghost_LF);
                    break;
                case Conversation.ID.Ghost_SH:
                    LoadEventsFromFile(self, EventID.Ghost_SH);
                    break;
                case Conversation.ID.Ghost_UW:
                    LoadEventsFromFile(self, EventID.Ghost_UW);
                    break;
                case Conversation.ID.Ghost_SB:
                    LoadEventsFromFile(self, EventID.Ghost_SB);
                    break;
                default: orig.Invoke(self); break;
            }
        }

        /// <summary>
        /// Translator for ShortStrings
        /// </summary>
        public static string Translate(string text)
        { // Add Translator Here!
            if (lastLang != curLang)
            { // Reload Translation
                LoadTable();
                lastLang = curLang;
            }
            if (curLang != LanguageID.English && ShortStringTable.Count > 0)
            {
                if (ShortStringTable.TryGetValue(text, out string trans)) { text = trans; }
                else { Debug.LogError($"Drought) {curLang} translation not found for [{text}](len: {text.Length})"); }
            }
            return text.Replace("\\n", Environment.NewLine);
        }

        private static Dictionary<string, string> ShortStringTable;

        private const int sstDisplace = 12467; // Salt for ShortString Encrypt

        private static string[] SplitByLines(string orig)
        {
            if (orig.Contains(Environment.NewLine)) { return Regex.Split(orig, Environment.NewLine); }
            else { return orig.Split(new char[] { '\n' }); }
        }

        private static void LoadTable()
        {
            ShortStringTable = new Dictionary<string, string>();
            if (curLang == LanguageID.English) { return; }
            string path = string.Concat(
                ResourceManager.assetDir,
                "Text",
                Path.DirectorySeparatorChar,
                "Short_Strings",
                Path.DirectorySeparatorChar,
                LangShort(curLang),
                ".txt"
                );
            Debug.Log($"Drought) TextManager LoadTable for {curLang}: {path}");
            if (!File.Exists(path)) { return; }
            string text = File.ReadAllText(path, Encoding.UTF8);
            if (text[0] == '0')
            { EncryptAllDialogue(); text = text.Remove(0, 1); }
            else
            { text = Crypto.DecryptStringAES(text.Remove(0, 1), sstDisplace); }
            string[] array = SplitByLines(text);
            for (int i = 1; i < array.Length; i++)
            {
                if (array[i].StartsWith("//")) { continue; }
                string item = array[i];
                if (item.Contains("//")) { item = item.Substring(0, item.IndexOf("//")).TrimEnd(); }
                string[] pair = item.Split(new char[] { '|' });
                if (pair.Length < 2) { continue; }
                if (ShortStringTable.ContainsKey(pair[0])) { continue; } // duplicate key
                ShortStringTable.Add(pair[0], pair[1]);
                // Debug.Log($"{ShortStringTable.Count}: {pair[0]}|{pair[1]}");
            }
        }

        private static void EncryptAllDialogue()
        {
            Debug.Log("Drought) encrypt all dialogue");
            // Short_Strings
            DirectoryInfo directoryInfo = new DirectoryInfo(string.Concat(
            ResourceManager.assetDir,
            Path.DirectorySeparatorChar,
            "Text",
            Path.DirectorySeparatorChar,
            "Short_Strings",
            Path.DirectorySeparatorChar
            ));
            FileInfo[] files = directoryInfo.GetFiles();
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].Name.Substring(files[i].Name.Length - 4, 4) == ".txt")
                {
                    string path = string.Concat(
                    ResourceManager.assetDir,
                    "Text",
                    Path.DirectorySeparatorChar,
                    "Short_Strings",
                    Path.DirectorySeparatorChar,
                    files[i].Name
                    );
                    string text = File.ReadAllText(path, Encoding.UTF8);
                    if (text[0] == '0')
                    {
                        string text2 = Crypto.EncryptStringAES(text.Remove(0, 1), sstDisplace);
                        text2 = '1' + text2;
                        Debug.Log("encrypting short string: " + files[i].Name);
                        File.WriteAllText(path, text2, Encoding.UTF8);
                    }
                }
            }
            // Event
            int maxEvent = Mathf.Max((int[])Enum.GetValues(typeof(EventID)));
            for (int j = 0; j < Enum.GetNames(typeof(LanguageID)).Length - 1; j++)
            {
                for (int k = 1; k <= maxEvent; k++)
                {
                    string path = string.Concat(
                    ResourceManager.assetDir,
                    Path.DirectorySeparatorChar,
                    "Text",
                    Path.DirectorySeparatorChar,
                    "Text_",
                    LangShort((LanguageID)j),
                    Path.DirectorySeparatorChar,
                    k,
                    ".txt"
                    );
                    if (File.Exists(path))
                    {
                        string text = File.ReadAllText(path, Encoding.UTF8);
                        if (text[0] == '0' && Regex.Split(text, "\r\n").Length > 1)
                        {
                            string text3 = Crypto.EncryptStringAES(text.Remove(0, 1), 54 + k + j * 7);
                            text3 = '1' + text3;
                            File.WriteAllText(path, text3, Encoding.UTF8);
                        }
                    }
                }
            }
            Debug.Log("encrypted event from 1 to " + maxEvent);
        }

        /// <summary>
        /// Call this when it's necessary
        /// </summary>
        public static void DecryptAllDialogue()
        {
            Debug.Log("Drought) decrypt all dialogue");
            // Short_Strings
            DirectoryInfo directoryInfo = new DirectoryInfo(string.Concat(
            ResourceManager.assetDir,
            Path.DirectorySeparatorChar,
            "Text",
            Path.DirectorySeparatorChar,
            "Short_Strings",
            Path.DirectorySeparatorChar
            ));
            FileInfo[] files = directoryInfo.GetFiles();
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].Name.Substring(files[i].Name.Length - 4, 4) == ".txt")
                {
                    string path = string.Concat(
                    ResourceManager.assetDir,
                    "Text",
                    Path.DirectorySeparatorChar,
                    "Short_Strings",
                    Path.DirectorySeparatorChar,
                    files[i].Name
                    );
                    string text = File.ReadAllText(path, Encoding.UTF8);
                    if (text[0] == '1')
                    {
                        string text2 = Crypto.DecryptStringAES(text.Remove(0, 1), sstDisplace);
                        text2 = '0' + text2;
                        Debug.Log($"Drought) decrypting short string {files[i].Name}");
                        File.WriteAllText(path, text2, Encoding.UTF8);
                    }
                }
            }
            // Event
            int maxEvent = Mathf.Max((int[])Enum.GetValues(typeof(EventID)));
            for (int j = 0; j < Enum.GetNames(typeof(LanguageID)).Length - 1; j++)
            {
                for (int k = 1; k <= maxEvent; k++)
                {
                    string path = string.Concat(
                    ResourceManager.assetDir,
                    Path.DirectorySeparatorChar,
                    "Text",
                    Path.DirectorySeparatorChar,
                    "Text_",
                    LangShort((LanguageID)j),
                    Path.DirectorySeparatorChar,
                    k,
                    ".txt"
                    );
                    if (File.Exists(path))
                    {
                        string text = File.ReadAllText(path, Encoding.UTF8);
                        if (text[0] == '1')
                        {
                            string text3 = Crypto.DecryptStringAES(text.Remove(0, 1), 54 + k + j * 7);
                            text3 = '0' + text3;
                            Debug.Log($"Drought) decrypting {(LanguageID)j} string {k}");
                            File.WriteAllText(path, text3, Encoding.UTF8);
                        }
                    }
                }
            }
        }

        public static void LoadEventsFromFile(Conversation instance, EventID id) => LoadEventsFromFile(instance, id, false, 0);

        public static void LoadEventsFromFile(Conversation instance, EventID id, bool oneRandomLine, int randomSeed)
        {
            Debug.Log($"~~~LOAD DROUGHT CONVO {id} ({(int)id})");
            LanguageID thisLang = curLang;
            string path = string.Concat(
                ResourceManager.assetDir,
                "Text",
                Path.DirectorySeparatorChar,
                "Text_",
                LangShort(curLang),
                Path.DirectorySeparatorChar,
                (int)id,
                ".txt"
            );
            if (!File.Exists(path))
            { // Use English if there's no translation
                thisLang = LanguageID.English;
                path = string.Concat(
                    ResourceManager.assetDir,
                    "Text",
                    Path.DirectorySeparatorChar,
                    "Text_Eng",
                    Path.DirectorySeparatorChar,
                    (int)id,
                    ".txt"
                );
                if (!File.Exists(path))
                {
                    Debug.Log("NOT FOUND " + path);
                    instance.events.Add(new Conversation.TextEvent(instance, 0, "NOT FOUND " + path, 100));
                    return;
                }
            }
            string text2 = File.ReadAllText(path, Encoding.UTF8);
            if (text2[0] == '0')
            { EncryptAllDialogue(); text2 = text2.Remove(0, 1); }
            else
            { text2 = Crypto.DecryptStringAES(text2.Remove(0, 1), (54 + (int)id + (int)thisLang * 7)); }
            string[] array = SplitByLines(text2);
            try
            {
                if (Regex.Split(array[0], "-")[1] == ((int)id).ToString())
                {
                    if (oneRandomLine)
                    {
                        List<Conversation.TextEvent> list = new List<Conversation.TextEvent>();
                        for (int i = 1; i < array.Length; i++)
                        {
                            string[] array2 = Regex.Split(array[i], " : ");
                            if (array2.Length == 3)
                            {
                                list.Add(new Conversation.TextEvent(instance, int.Parse(array2[0]), array2[2], int.Parse(array2[1])));
                            }
                            else if (array2.Length == 1 && array2[0].Length > 0)
                            {
                                list.Add(new Conversation.TextEvent(instance, 0, array2[0], 0));
                            }
                        }
                        if (list.Count > 0)
                        {
                            int seed = UnityEngine.Random.seed;
                            UnityEngine.Random.seed = randomSeed;
                            Conversation.TextEvent item = list[UnityEngine.Random.Range(0, list.Count)];
                            UnityEngine.Random.seed = seed;
                            instance.events.Add(item);
                        }
                    }
                    else
                    {
                        for (int j = 1; j < array.Length; j++)
                        {
                            string[] array3 = Regex.Split(array[j], " : ");
                            if (array3.Length == 3)
                            {
                                instance.events.Add(new Conversation.TextEvent(instance, int.Parse(array3[0]), array3[2], int.Parse(array3[1])));
                            }
                            else if (array3.Length == 2)
                            {
                                if (array3[0] == "SPECEVENT")
                                { instance.events.Add(new Conversation.SpecialEvent(instance, 0, array3[1])); }
                                else if (array3[0] == "PEBBLESWAIT")
                                { instance.events.Add(new SSOracleBehavior.PebblesConversation.PauseAndWaitForStillEvent(instance, null, int.Parse(array3[1]))); }
                            }
                            else if (array3.Length == 1 && array3[0].Length > 0)
                            { instance.events.Add(new Conversation.TextEvent(instance, 0, array3[0], 0)); }
                        }
                    }
                }
            }
            catch
            {
                Debug.Log("TEXT ERROR");
                instance.events.Add(new Conversation.TextEvent(instance, 0, "TEXT ERROR", 100));
            }
        }

        public enum EventID
        {
            // GhostConversation
            Ghost_CC = 1,
            Ghost_SI = 2,
            Ghost_LF = 3,
            Ghost_SH = 4,
            Ghost_UW = 5,
            Ghost_SB = 6,
            // PearlConversation
            PC_Moon_Pearl_CC = 7,
            PC_Moon_Pearl_SI_west = 20,
            PC_Moon_Pearl_SI_top = 21,
            PC_Moon_Pearl_LF_west = 10,
            PC_Moon_Pearl_LF_bottom = 11,
            PC_Moon_Pearl_HI = 12,
            PC_Moon_Pearl_SH = 13,
            PC_Moon_Pearl_DS = 14,
            PC_Moon_Pearl_SB_filtration = 15,
            PC_Moon_Pearl_GW = 16,
            PC_Moon_Pearl_SL_bridge = 17,
            PC_Moon_Pearl_SL_moon = 18,
            PC_Moon_Pearl_SU = 41,
            PC_Moon_Pearl_SB_ravine = 43,
            PC_Moon_Pearl_UW = 42,
            PC_Moon_Pearl_SL_chimney = 54,
            PC_SI_Spire1 = 22,
            PC_SI_Spire2 = 23,
            PC_SI_Spire3 = 24,
            PC_MiscPearl = 38,
            PC_PebblesPearl = 40,
            PC_Moon_Pearl_Drought1_IS = 76,
            PC_Moon_Pearl_Drought2_FS = 77,
            PC_Moon_Pearl_Drought3_MW = 78,
            // LMOracleBehaviorHasMark (duplicate of PearlConversation)
            LM_Moon_Pearl_CC = 7,
            LM_Moon_Pearl_SI_west = 20,
            LM_Moon_Pearl_SI_top = 21,
            LM_Moon_Pearl_LF_west = 10,
            LM_Moon_Pearl_LF_bottom = 11,
            LM_Moon_Pearl_HI = 12,
            LM_Moon_Pearl_SH = 13,
            LM_Moon_Pearl_DS = 14,
            LM_Moon_Pearl_SB_filtration = 15,
            LM_Moon_Pearl_GW = 16,
            LM_Moon_Pearl_SL_bridge = 17,
            LM_Moon_Pearl_SL_moon = 18,
            LM_Moon_Pearl_SU = 41,
            LM_Moon_Pearl_SB_ravine = 43,
            LM_Moon_Pearl_UW = 42,
            LM_Moon_Pearl_SL_chimney = 54,
            LM_SI_Spire1 = 22,
            LM_SI_Spire2 = 23,
            LM_SI_Spire3 = 24,
            LM_MiscPearl = 38,
            LM_PebblesPearl = 40,
            LM_Moon_Pearl_Drought1_IS = 76,
            LM_Moon_Pearl_Drought2_FS = 77,
            LM_Moon_Pearl_Drought3_MW = 78,
            // LMOracleBehaviorHasMark
            LM_KarmaFlower = 25,
            LM_SSOracleSwarmer = 19,
            LM_DangleFruit = 26,
            LM_FlareBomb = 27,
            LM_VultureMask = 28,
            LM_PuffBall = 29,
            LM_JellyFish = 30,
            LM_Lantern = 31,
            LM_Mushroom = 32,
            LM_FirecrackerPlant = 33,
            LM_SlimeMold = 34,
            LM_ScavBomb = 44,
            LM_BubbleGrass = 53,
            LM_OverseerRemains = 52,
            LM_Moon_Pearl_Red_stomach = 51, // Unused in Drought
            LM_Moon_Red_First_Conversation = 50, // Replaced by FP_SlimeMold
            LM_Moon_Red_Second_Conversation = 55, // Replaced by FP_Moon_Pearl_LF_west
            LM_Moon_Yellow_First_Conversation = 49, // Replaced by FP_FirecrackerPlant
            LM_MoonFirstPostMarkConversation = 79,
            // FPOracleBehaviorHasMark
            FP_FirstPostMarkConversation_Betrayed = 80,
            FP_FirstPostMarkConversation = 81,
            FP_SecondPostMarkConversation_Betrayed = 82,
            FP_Moon_Pearl_CC = 8,
            FP_Moon_Pearl_SI_west = 64,
            FP_Moon_Pearl_SI_top = 65,
            FP_Moon_Pearl_LF_west = 55,
            FP_Moon_Pearl_LF_bottom = 56,
            FP_Moon_Pearl_HI = 57,
            FP_Moon_Pearl_SH = 58,
            FP_Moon_Pearl_DS = 59,
            FP_Moon_Pearl_SB_filtration = 60,
            FP_Moon_Pearl_GW = 61,
            FP_Moon_Pearl_SL_bridge = 62,
            FP_Moon_Pearl_SL_moon = 63,
            FP_Moon_Pearl_SU = 69,
            FP_Moon_Pearl_SB_ravine = 60,
            FP_Moon_Pearl_UW = 70,
            FP_Moon_Pearl_SL_chimney = 62,
            FP_SI_Spire1 = 66,
            FP_SI_Spire2 = 67,
            FP_SI_Spire3 = 68,
            FP_MiscPearl = 74,
            FP_MoonPearl = 73,
            FP_Moon_Pearl_Red_stomach = 51, // Unused in Drought
            FP_Moon_Red_First_Conversation = 50, // Replaced by FP_SlimeMold
            FP_Moon_Red_Second_Conversation = 55, // Replaced by FP_Moon_Pearl_LF_west
            FP_Moon_Yellow_First_Conversation = 49, // Replaced by FP_FirecrackerPlant
            FP_KarmaFlower = 35,
            FP_DangleFruit = 36,
            FP_FlareBomb = 37,
            FP_VultureMask = 39,
            FP_PuffBall = 45,
            FP_JellyFish = 46,
            FP_Lantern = 47,
            FP_Mushroom = 48,
            FP_FirecrackerPlant = 49,
            FP_SlimeMold = 50,
            FP_ScavBomb = 75,
            FP_BubbleGrass = 53,
            FP_OverseerRemains = 52,
            FP_Moon_Pearl_Drought1_IS = 83,
            FP_Moon_Pearl_Drought2_FS = 84,
            FP_Moon_Pearl_Drought3_MW = 85,
            // SRS MessageScreen
            SRSDreamPearlLF2 = 86,
            SRSDreamPearlHI = 87,
            SRSDreamPearlSH = 88,
            SRSDreamPearlSB = 89,
            SRSDreamPearlSB2 = 90,
            SRSDreamPearlGW = 91,
            SRSDreamPearlSL = 92,
            SRSDreamPearlSL2 = 93,
            SRSDreamPearlSL3 = 94,
            SRSDreamPearlSI2 = 95,
            SRSDreamPearlSI5 = 96,
            SRSDreamPearlFS = 97,
            SRSDreamTraitor = 98,
            SRSDreamMissonComplete = 99
        }
    }
}
