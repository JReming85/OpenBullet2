﻿using Newtonsoft.Json;
using RuriLib.Functions.Conversion;
using RuriLib.Functions.Crypto;
using RuriLib.Helpers.Transpilers;
using RuriLib.Models.Blocks;
using RuriLib.Models.Blocks.Custom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RuriLib.Models.Configs
{
    public class Config
    {
        public string Id { get; set; }
        public bool IsRemote { get; set; } = false;
        public ConfigMode Mode { get; set; } = ConfigMode.Stack;
        public ConfigMetadata Metadata { get; set; } = new ConfigMetadata();
        public ConfigSettings Settings { get; set; } = new ConfigSettings();
        public string Readme { get; set; } = "Type some **markdown** here";
        
        public List<BlockInstance> Stack { get; set; } = new List<BlockInstance>();
        public string LoliCodeScript { get; set; } = "";
        public string CSharpScript { get; set; } = "";
        public byte[] DLLBytes { get; set; } = Array.Empty<byte>();

        // Hashes used to check if the config was saved
        private string loliCodeHash;
        private string cSharpHash;

        [JsonIgnore]
        public List<(BlockInstance, int)> DeletedBlocksHistory { get; set; } = new List<(BlockInstance, int)>();

        public Config()
        {
            Id = Guid.NewGuid().ToString();
        }

        public void ChangeMode(ConfigMode newMode)
        {
            if (newMode == Mode)
                return;

            var mappings = new Dictionary<(ConfigMode, ConfigMode), Action>
            {
                { (ConfigMode.Stack, ConfigMode.LoliCode), () => LoliCodeScript = Stack2LoliTranspiler.Transpile(Stack) },
                { (ConfigMode.Stack, ConfigMode.CSharp), () => CSharpScript = Stack2CSharpTranspiler.Transpile(Stack, Settings) },
                { (ConfigMode.LoliCode, ConfigMode.Stack), () => Stack = Loli2StackTranspiler.Transpile(LoliCodeScript) },
                { (ConfigMode.LoliCode, ConfigMode.CSharp), () => CSharpScript = Loli2CSharpTranspiler.Transpile(LoliCodeScript, Settings) }
            };

            if (mappings.ContainsKey((Mode, newMode)))
            {
                mappings[(Mode, newMode)].Invoke();
                Mode = newMode;
            }
            else
            {
                throw new Exception($"Cannot convert mode from {Mode} to {newMode}");
            }
        }

        /// <summary>
        /// Checks if the config has only blocks or also additional C# code
        /// </summary>
        public bool HasCSharpCode()
        {
            try
            {
                return Mode switch
                {
                    ConfigMode.CSharp => true,
                    ConfigMode.DLL => true,
                    ConfigMode.Stack => Stack.Any(b => IsDangerousBlock(b)),
                    ConfigMode.LoliCode => Loli2StackTranspiler.Transpile(LoliCodeScript).Any(b => IsDangerousBlock(b)),
                    _ => throw new NotImplementedException(),
                };
            }
            catch
            {
                // We don't know, return false just to avoid false positives
                return false;
            }
        }

        private static bool IsDangerousBlock(BlockInstance b)
            => b is LoliCodeBlockInstance || b is ScriptBlockInstance || b.Descriptor.Id == "ShellCommand";

        /// <summary>
        /// Update the hashes of the current state of LoliCode and C# scripts
        /// (call this this when you first load the config or when you save changes to the repository).
        /// </summary>
        public void UpdateHashes()
        {
            loliCodeHash = GetHash(LoliCodeScript + JsonConvert.SerializeObject(Settings));
            cSharpHash = GetHash(CSharpScript + JsonConvert.SerializeObject(Settings));
        }

        /// <summary>
        /// Checks if the config's LoliCode or C# code have been edited since
        /// the last call of <see cref="UpdateHashes"/>.
        /// </summary>
        public bool HasUnsavedChanges()
            => Mode == ConfigMode.CSharp
                ? GetHash(CSharpScript + JsonConvert.SerializeObject(Settings)) != cSharpHash
                : GetHash(LoliCodeScript + JsonConvert.SerializeObject(Settings)) != loliCodeHash;

        private static string GetHash(string str)
            => HexConverter.ToHexString(Crypto.SHA1(Encoding.UTF8.GetBytes(str)));
    }
}
