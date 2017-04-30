﻿using System;
using System.IO;
using System.Text;
using System.Timers;

namespace STARK {
    class CommandReader : IDisposable{

        Command synthCmd;
        Command playCmd;
        Command pauseCmd;
        Command resumeCmd;
        Command stopCmd;
        Command skipCurrentCmd;
        Command clearQueueCmd;
        Command blockUserCmd;
        Command blockWordCmd;

        Timer loop; //no, you can't use FileSystemWatcher, it doesn't work. I tried.
        StreamReader reader;

        SourceGame selectedGame;
        string logFile = "";

        QueuedSpeechSynthesizer qss;
        AudioPlaybackEngine ape;
        AudioFileManager afm;

        bool changingPath = false;

        public CommandReader(ref QueuedSpeechSynthesizer qss, ref AudioPlaybackEngine ape, ref AudioFileManager afm, SourceGame selectedGame) {
            this.selectedGame = selectedGame;
            this.logFile = PathManager.steamApps + @"\common\Team Fortress 2\tf\!tts-axynos.txt";
            this.qss = qss;
            this.ape = ape;
            this.afm = afm;

            this.synthCmd = CommandManager.synthCmd;
            playCmd = CommandManager.playCmd;
            pauseCmd = CommandManager.pauseCmd;
            resumeCmd = CommandManager.resumeCmd;
            stopCmd = CommandManager.stopCmd;
            skipCurrentCmd = CommandManager.skipCurrentCmd;
            clearQueueCmd = CommandManager.clearQueueCmd;
            blockUserCmd = CommandManager.blockUserCmd;
            blockWordCmd = CommandManager.blockWordCmd;

            Setup(logFile);
            StartReadLoop();
        }

        public static Encoding GetEncoding(string filename)
        {
            // Read the BOM
            var bom = new byte[4];
            using (var file = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 4);
            }

            // Analyze the BOM
            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76) return Encoding.UTF7;
            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return Encoding.UTF32;
            return Encoding.Default;
        }

        private void parseCommand(string line) {
            if (line != "") {
                if (ContainsCommand(line, synthCmd)) {
                    if (qss != null) {
                        var parts = getParts(line, synthCmd);
                        string prompt = parts[1];
                        string player = getPlayer(parts[0]);

                        string[] blocked_users = File.ReadAllLines("blocked_users.txt");
                        string[] blocked_words = File.ReadAllLines("blocked_words.txt");
                        string[] whitelisted_users = File.ReadAllLines("whitelisted_users.txt");
                        string[] replace = File.ReadAllLines("replace.txt");
                        string lowercasePrompt = prompt.ToLower();
                        bool blockedUser = false;
                        bool blockedWord = false;
                        int whitelistedUser = 0;
                        string replacement = lowercasePrompt;

                        if (MainWindow.whitelistedOnlyTTS == true)
                        {
                            for (int i = 0; i <= whitelisted_users.Length - 1; i++)
                            {
                                if (player.Contains(whitelisted_users[i]))
                                {
                                    whitelistedUser++;
                                }
                                if (whitelistedUser == 1)
                                {
                                    qss.AddToQueue(new QSSQueueItem(prompt, player));
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i <= blocked_users.Length - 1; i++)
                            {
                                if (player.Contains(blocked_users[i]))
                                {
                                    blockedUser = true;
                                }
                            }

                            for (int i = 0; i <= blocked_words.Length - 1; i++)
                            {
                                string lowercaseBlocked_word = blocked_words[i].ToLower();

                                if (lowercasePrompt.Contains(lowercaseBlocked_word))
                                {
                                    blockedWord = true;
                                }
                            }

                            for (int i = 0; i <= replace.Length - 1; i++)
                            {
                                string[] replaceThing = replace[i].Split('=');

                                if (replaceThing.Length == 2)
                                {
                                    string replaceTrigger = replaceThing[0].ToLower();
                                    string replaceWith = replaceThing[1];

                                    if (lowercasePrompt.Contains(replaceTrigger))
                                    {
                                        replacement = replacement.Replace(replaceTrigger, replaceWith);
                                    }
                                }
                            }

                            if (blockedUser == false)
                            {
                                if (blockedWord == false)
                                {
                                    prompt = replacement;
                                    qss.AddToQueue(new QSSQueueItem(prompt, player));
                                }
                            }
                        }
                    }
                }
                else if (ContainsCommand(line, playCmd)) {
                    if (MainWindow.whitelistedOnlyPlayCmd == true)
                    {
                        var parts = getParts(line, synthCmd);
                        string player = getPlayer(parts[0]);
                        string[] whitelisted_users = File.ReadAllLines("whitelisted_users.txt");
                        int whitelistedUser = 0;

                        for (int i = 0; i <= whitelisted_users.Length - 1; i++)
                        {
                            if (player.Contains(whitelisted_users[i]))
                            {
                                whitelistedUser++;
                            }
                            if (whitelistedUser == 1)
                            {
                                TryParsePlay(line);
                            }
                        }
                    }
                    else
                    {
                        TryParsePlay(line);
                    }
                }
                else if (ContainsCommand(line, pauseCmd)) {
                    if (MainWindow.whitelistedOnlyPauseCmd == true)
                    {
                        var parts = getParts(line, synthCmd);
                        string player = getPlayer(parts[0]);
                        string[] whitelisted_users = File.ReadAllLines("whitelisted_users.txt");
                        int whitelistedUser = 0;

                        for (int i = 0; i <= whitelisted_users.Length - 1; i++)
                        {
                            if (player.Contains(whitelisted_users[i]))
                            {
                                whitelistedUser++;
                            }
                            if (whitelistedUser == 1)
                            {
                                ape.Pause();
                            }
                        }
                    }
                    else
                    {
                        ape.Pause();
                    }
                }
                else if (ContainsCommand(line, resumeCmd)) {
                    if (MainWindow.whitelistedOnlyResumeCmd == true)
                    {
                        var parts = getParts(line, synthCmd);
                        string player = getPlayer(parts[0]);
                        string[] whitelisted_users = File.ReadAllLines("whitelisted_users.txt");
                        int whitelistedUser = 0;

                        for (int i = 0; i <= whitelisted_users.Length - 1; i++)
                        {
                            if (player.Contains(whitelisted_users[i]))
                            {
                                whitelistedUser++;
                            }
                            if (whitelistedUser == 1)
                            {
                                ape.Resume();
                            }
                        }
                    }
                    else
                    {
                        ape.Resume();
                    }
                }
                else if (ContainsCommand(line, stopCmd)) {
                    if (MainWindow.whitelistedOnlyStopCmd == true)
                    {
                        var parts = getParts(line, synthCmd);
                        string player = getPlayer(parts[0]);
                        string[] whitelisted_users = File.ReadAllLines("whitelisted_users.txt");
                        int whitelistedUser = 0;

                        for (int i = 0; i <= whitelisted_users.Length - 1; i++)
                        {
                            if (player.Contains(whitelisted_users[i]))
                            {
                                whitelistedUser++;
                            }
                            if (whitelistedUser == 1)
                            {
                                ape.Stop();
                            }
                        }
                    }
                    else
                    {
                        ape.Stop();
                    }
                }
                else if (ContainsCommand(line, skipCurrentCmd))
                {
                    if (MainWindow.whitelistedOnlySkipCurrentCmd == true)
                    {
                        var parts = getParts(line, synthCmd);
                        string player = getPlayer(parts[0]);
                        string[] whitelisted_users = File.ReadAllLines("whitelisted_users.txt");
                        int whitelistedUser = 0;

                        for (int i = 0; i <= whitelisted_users.Length - 1; i++)
                        {
                            if (player.Contains(whitelisted_users[i]))
                            {
                                whitelistedUser++;
                            }
                            if (whitelistedUser == 1)
                            {
                                qss.SkipCurrent();
                            }
                        }
                    }
                    else
                    {
                        qss.SkipCurrent();
                    }
                }
                else if (ContainsCommand(line, clearQueueCmd))
                {
                    if (MainWindow.whitelistedOnlyClearQueueCmd == true)
                    {
                        var parts = getParts(line, synthCmd);
                        string player = getPlayer(parts[0]);
                        string[] whitelisted_users = File.ReadAllLines("whitelisted_users.txt");
                        int whitelistedUser = 0;

                        for (int i = 0; i <= whitelisted_users.Length - 1; i++)
                        {
                            if (player.Contains(whitelisted_users[i]))
                            {
                                whitelistedUser++;
                            }
                            if (whitelistedUser == 1)
                            {
                                qss.Clear();
                            }
                        }
                    }
                    else
                    {
                        qss.Clear();
                    }
                }
                if (ContainsCommand(line, blockUserCmd))
                {
                    var parts = getParts(line, blockUserCmd);
                    string prompt = parts[1];
                    string player = getPlayer(parts[0]);

                    string blocked_users = File.ReadAllText("blocked_users.txt");
                    string[] whitelisted_users = File.ReadAllLines("whitelisted_users.txt");
                    int whitelistedUser = 0;

                    if (MainWindow.whitelistedOnlyBlockUserCmd == true)
                    {
                        for (int i = 0; i <= whitelisted_users.Length - 1; i++)
                        {
                            if (player.Contains(whitelisted_users[i]))
                            {
                                whitelistedUser++;
                            }
                            if (whitelistedUser == 1)
                            {
                                var encodingForFile = GetEncoding("blocked_users.txt");
                                string lastline = string.Empty;

                                string[] Lines = blocked_users.Split('\n');

                                if (Lines.Length == 0)
                                {
                                    lastline = string.Empty;
                                }
                                else if (Lines.Length >= 1)
                                {
                                    lastline = Lines[Lines.Length - 1];
                                }

                                if (lastline.Length == 0)
                                {
                                    using (StreamWriter sw = new StreamWriter(File.Open("blocked_users.txt", FileMode.Append), encodingForFile))
                                    {
                                        sw.WriteLine(prompt);
                                    }
                                }
                                else
                                {
                                    using (StreamWriter sw = new StreamWriter(File.Open("blocked_users.txt", FileMode.Append), encodingForFile))
                                    {
                                        sw.WriteLine();
                                        sw.WriteLine(prompt);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        var encodingForFile = GetEncoding("blocked_users.txt");
                        string lastline = string.Empty;

                        string[] Lines = blocked_users.Split('\n');

                        if (Lines.Length == 0)
                        {
                            lastline = string.Empty;
                        }
                        else if (Lines.Length >= 1)
                        {
                            lastline = Lines[Lines.Length - 1];
                        }

                        if (lastline.Length == 0)
                        {
                            using (StreamWriter sw = new StreamWriter(File.Open("blocked_users.txt", FileMode.Append), encodingForFile))
                            {
                                sw.WriteLine(prompt);
                            }
                        }
                        else
                        {
                            using (StreamWriter sw = new StreamWriter(File.Open("blocked_users.txt", FileMode.Append), encodingForFile))
                            {
                                sw.WriteLine();
                                sw.WriteLine(prompt);
                            }
                        }
                    }
                }
                if (ContainsCommand(line, blockWordCmd))
                {
                    var parts = getParts(line, blockWordCmd);
                    string prompt = parts[1];
                    string player = getPlayer(parts[0]);

                    string blocked_words = File.ReadAllText("blocked_words.txt");
                    string[] whitelisted_users = File.ReadAllLines("whitelisted_users.txt");
                    int whitelistedUser = 0;

                    if (MainWindow.whitelistedOnlyBlockWordCmd == true)
                    {
                        for (int i = 0; i <= whitelisted_users.Length - 1; i++)
                        {
                            if (player.Contains(whitelisted_users[i]))
                            {
                                whitelistedUser++;
                            }
                            if (whitelistedUser == 1)
                            {
                                var encodingForFile = GetEncoding("blocked_words.txt");
                                string lastline = string.Empty;

                                string[] Lines = blocked_words.Split('\n');

                                if (Lines.Length == 0)
                                {
                                    lastline = string.Empty;
                                }
                                else if (Lines.Length >= 1)
                                {
                                    lastline = Lines[Lines.Length - 1];
                                }

                                if (lastline.Length == 0)
                                {
                                    using (StreamWriter sw = new StreamWriter(File.Open("blocked_words.txt", FileMode.Append), encodingForFile))
                                    {
                                        sw.WriteLine(prompt);
                                    }
                                }
                                else
                                {
                                    using (StreamWriter sw = new StreamWriter(File.Open("blocked_words.txt", FileMode.Append), encodingForFile))
                                    {
                                        sw.WriteLine();
                                        sw.WriteLine(prompt);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        var encodingForFile = GetEncoding("blocked_words.txt");
                        string lastline = string.Empty;

                        string[] Lines = blocked_words.Split('\n');

                        if (Lines.Length == 0)
                        {
                            lastline = string.Empty;
                        }
                        else if (Lines.Length >= 1)
                        {
                            lastline = Lines[Lines.Length - 1];
                        }

                        if (lastline.Length == 0)
                        {
                            using (StreamWriter sw = new StreamWriter(File.Open("blocked_words.txt", FileMode.Append), encodingForFile))
                            {
                                sw.WriteLine(prompt);
                            }
                        }
                        else
                        {
                            using (StreamWriter sw = new StreamWriter(File.Open("blocked_words.txt", FileMode.Append), encodingForFile))
                            {
                                sw.WriteLine();
                                sw.WriteLine(prompt);
                            }
                        }
                    }
                }
            }
        }

        private void StartReadLoop() {
            loop = new Timer(50);
            loop.AutoReset = false;
            loop.Elapsed += Loop_Elapsed;
            loop.Start();
        }

        private void Setup(string path) {
            //makes a file if there is none
            if (Directory.Exists(PathManager.steamApps + @"\common\Team Fortress 2\tf\cfg")) {
                try {
                    if (!File.Exists(path)) File.CreateText(path).Close();
                    var bufferedStream = new BufferedStream(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                    reader = new StreamReader(bufferedStream);
                    //skips the lines that are in the file on load so we don't get historic commands
                    reader.ReadToEnd();
                }
                catch (IOException e) {

                }
            } else {
                App.Current.Dispatcher.Invoke(delegate {
                    (App.Current.MainWindow as MainWindow).setCmdReadertoNull();
                });
            }
        }

        private bool ContainsCommand(string line, Command command) {
            if (line.Contains(command.getSplitter()[0]) || line.Contains(command.getSplitter()[1])) return true;
            else return false;
        }

        private void Loop_Elapsed(object sender, ElapsedEventArgs e) {
            if (changingPath == false && File.Exists(logFile)) {
                //goes through lines that don't interest us at once, no need to loop again between each line
                while (true) {
                    var line = reader.ReadLine();
                    if (line == null) break;
                    parseCommand(line);
                }
            }
            loop.Start();
        }

        private void TryParsePlay(string line) {
            if (ape != null) {
                var parts = getParts(line, playCmd);
                string player = getPlayer(parts[0]);
                string arg = parts[1];

                Func<bool> tryPlayByTitle = () => {
                    if (!string.IsNullOrEmpty(arg) && !string.IsNullOrWhiteSpace(arg)) {
                        foreach (AudioPlaybackItem item in afm.getCollection()) {
                            if (item.name.ToLower() == arg.ToLower()) {
                                return true;
                            }
                        }
                    }

                    return false;
                };
                bool canPlayByTitle = tryPlayByTitle();

                Func<bool> tryPlayByTag = () => {
                    if (!string.IsNullOrEmpty(arg) && !string.IsNullOrWhiteSpace(arg)) {
                        foreach (AudioPlaybackItem item in afm.getCollection()) {
                            foreach (string tag in item.tags) {
                                if (tag.ToLower() == arg.ToLower()) {
                                    return true;
                                }
                            }
                        }
                    }


                    return false;
                };
                bool canPlayByTag = tryPlayByTag();

                string arg1 = new StringBuilder(parts[1]).ToString();
                int id;
                if (int.TryParse(arg1, out id) && id >= 0) {
                    if (id < afm.getCollection().Count) {
                        ape.Stop(); //you can't stop Harambe
                        ape.Play(id);
                    }
                } else if (canPlayByTitle) {
                    foreach (AudioPlaybackItem item in afm.getCollection()) {
                        if (item.name.ToLower() == parts[1].ToLower()) {
                            ape.Stop();
                            ape.Play(item.id);
                        }
                    }
                } else if (canPlayByTag) {
                    foreach (AudioPlaybackItem item in afm.getCollection()) {
                        foreach (string tag in item.tags) {
                            if (tag.ToLower() == arg.ToLower()) {
                                ape.Stop();
                                ape.Play(item.id);
                            }
                        }
                    }
                }

            }
        }

        

        //CHANGE METHODS
        #region "Change Methods"
        public void ChangePath(string path) {
            changingPath = true;
            if (File.Exists(path)) {
                reader.Close();
                this.logFile = path;
                Setup(path);
            }
            changingPath = false;
        }

        public void ChangeSynthCommand(string newCmd) {
            synthCmd.changeCommand(newCmd);
        }

        public void ChangeSelectedGame(SourceGame selectedGame) {
            if (selectedGame != null) {
                this.selectedGame = selectedGame;
            }
        }
        #endregion

        //GETTERS
        #region "Getters"
        //use only with actual fucking parts[0], not some random shit, you fuck
        private string getPlayer(string input) {
            if (input.Contains("(Terrorist)")) return new StringBuilder(input).Remove(0, "(Terrorist)".Length-1).ToString();
            else if (input.Contains("(Counter-Terrorist)")) return new StringBuilder(input).Remove(0, "(Counter - Terrorist)".Length-1).ToString();

            return input;
        }

        private string[] getParts(string line, Command command) {
            return line.Split(command.getSplitter(), StringSplitOptions.None);
        }

        #endregion
        public void Dispose() {
            if (loop != null) loop.Dispose();
            if (reader != null) reader.Dispose();

            loop = null;
            reader = null;
        }
    }
}
