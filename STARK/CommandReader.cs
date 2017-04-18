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

            Setup(logFile);
            StartReadLoop();
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
                        int blockedUser = 0;
                        int blockedWord = 0;
                        int whitelistedUser = 0;

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
                                    blockedUser = 1;
                                }
                            }

                            for (int i = 0; i <= blocked_words.Length - 1; i++)
                            {
                                string lowercasePrompt = prompt.ToLower();
                                string lowercaseBlocked_word = blocked_words[i].ToLower();

                                if (lowercasePrompt.Contains(lowercaseBlocked_word))
                                {
                                    blockedWord = 1;
                                }
                            }

                            if (blockedUser == 0)
                            {
                                if (blockedWord == 0)
                                {
                                    qss.AddToQueue(new QSSQueueItem(prompt, player));
                                }
                            }
                        }
                    }
                }
                else if (ContainsCommand(line, playCmd)) {
                    if (MainWindow.whitelistedOnlyAudio == true)
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
                    if (MainWindow.whitelistedOnlyAudio == true)
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
                    if (MainWindow.whitelistedOnlyAudio == true)
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
                    if (MainWindow.whitelistedOnlyAudio == true)
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
