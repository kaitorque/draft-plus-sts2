using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace DraftPlus.Code;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "DraftPlus";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        Logger.Info("Draft+ initialized.");

        Harmony harmony = new(ModId);

        harmony.PatchAll();
    }
}
