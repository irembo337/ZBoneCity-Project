using System;
using System.IO;
using System.Reflection;
using MelonLoader;
using MelonLoader.Utils;

namespace BonelabAdvancedHealth
{
    public sealed class ExternalModCompatibilitySystem
    {
        private bool _logged;

        public bool BreakableBonesInstalled { get; private set; }
        public bool InjuriesInstalled { get; private set; }
        public bool NoDeathAnimationsInstalled { get; private set; }
        public bool AudioImportLibInstalled { get; private set; }
        public bool RagdollToggleInstalled { get; private set; }

        public void Initialize()
        {
            Probe();
            LogOnce();
        }

        public void Probe()
        {
            BreakableBonesInstalled = HasAssemblyOrFile("BreakableBones") || HasAssemblyOrFile("BreakableBonesPatch6");
            InjuriesInstalled = HasAssemblyOrFile("InjuriesMod") || HasAssemblyOrFile("Injuries");
            NoDeathAnimationsInstalled = HasAssemblyOrFile("NoDeathAnimations");
            AudioImportLibInstalled = HasAssemblyOrFile("AudioImportLib");
            RagdollToggleInstalled = HasAssemblyOrFile("RagdollToggle") || HasAssemblyOrFile("ToggleRagdoll");
        }

        public float GetExternalFractureDamping(HealthOwnerKind kind)
        {
            if (!BreakableBonesInstalled)
                return 1f;

            return kind == HealthOwnerKind.Player ? 0.78f : 0.90f;
        }

        public bool ShouldSuppressDeathAnimation()
        {
            return NoDeathAnimationsInstalled;
        }

        public bool CanUseExternalAudioImport()
        {
            return AudioImportLibInstalled;
        }

        private void LogOnce()
        {
            if (_logged)
                return;

            _logged = true;
            MainMod.Runtime?.Logger.Msg(
                "External mod compatibility: BreakableBones=" + BreakableBonesInstalled +
                ", Injuries=" + InjuriesInstalled +
                ", NoDeathAnimations=" + NoDeathAnimationsInstalled +
                ", AudioImportLib=" + AudioImportLibInstalled +
                ", RagdollToggle=" + RagdollToggleInstalled);
        }

        private static bool HasAssemblyOrFile(string name)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                string assemblyName = assemblies[i].GetName().Name ?? string.Empty;
                if (assemblyName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            string modsDir = Path.Combine(MelonEnvironment.GameRootDirectory, "Mods");
            if (!Directory.Exists(modsDir))
                return false;

            string[] files = Directory.GetFiles(modsDir, "*.dll");
            for (int i = 0; i < files.Length; i++)
            {
                string file = Path.GetFileNameWithoutExtension(files[i]);
                if (file.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
