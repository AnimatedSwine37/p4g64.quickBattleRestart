using System.Diagnostics;
using Reloaded.Mod.Interfaces;
using p4g64.quickBattleRestart.Template;
using p4g64.quickBattleRestart.Configuration;
using System.IO;
using System.Runtime.InteropServices;
using Reloaded.Hooks.Definitions;
using static p4g64.quickBattleRestart.Utils;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace p4g64.quickBattleRestart;

/// <summary>
/// Your mod logic goes here.
/// </summary>
public unsafe class Mod : ModBase // <= Do not Remove.
{
    /// <summary>
    /// Provides access to the mod loader API.
    /// </summary>
    private readonly IModLoader _modLoader;

    /// <summary>
    /// Provides access to the Reloaded.Hooks API.
    /// </summary>
    /// <remarks>This is null if you remove dependency on Reloaded.SharedLib.Hooks in your mod.</remarks>
    private readonly IReloadedHooks? _hooks;

    /// <summary>
    /// Provides access to the Reloaded logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Entry point into the mod, instance that created this class.
    /// </summary>
    private readonly IMod _owner;

    /// <summary>
    /// Provides access to this mod's configuration.
    /// </summary>
    private Config _configuration;

    /// <summary>
    /// The configuration of the currently executing mod.
    /// </summary>
    private readonly IModConfig _modConfig;

    private GetPressedInputDelegate _getPressedInput;
    private CanChangeToStateDelegate _canChangeToState;
    private IHook<ProcessInputsDelegate> _processInputsHook;
    private PartyInfo* _partyInfo;
    
    public Mod(ModContext context)
    {
        _modLoader = context.ModLoader;
        _hooks = context.Hooks;
        _logger = context.Logger;
        _owner = context.Owner;
        _configuration = context.Configuration;
        _modConfig = context.ModConfig;

        if (!Utils.Initialise(_logger, _configuration, _modLoader))
        {
            return;
        }

        if (_hooks == null)
        {
            LogError("Failed to get Reloaded Hooks, nothing will work!");
            return;
        }

        SigScan("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 40 48 8B D9 4D 8B F0",
            "Btl::Menu::Main::ProcessInputs",
            address =>
            {
                _processInputsHook = _hooks.CreateHook<ProcessInputsDelegate>(ProcessInputsHook, address).Activate();
            });

        SigScan("40 53 55 56 57 48 83 EC 78", "Btl::Menu::GetPressedInputs",
            address => { _getPressedInput = _hooks.CreateWrapper<GetPressedInputDelegate>(address, out _); });


        SigScan(
            "48 89 5C 24 ?? 57 48 83 EC 20 48 8B F9 0F B7 DA 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 4C 8B 05 ?? ?? ?? ??",
            "Btl::UI::Command::CanChangeToState",
            address => { _canChangeToState = _hooks.CreateWrapper<CanChangeToStateDelegate>(address, out _); });
        
        SigScan("48 8D 0D ?? ?? ?? ?? 48 8B D3 E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 81 FF 40 03 00 00", "PartyInfoPtr",
            address =>
            {
                _partyInfo = (PartyInfo*)GetGlobalAddress(address + 3);
            });
    }

    private int ProcessInputsHook(BtlCommandsInfo* commands, BtlInfo* btl, void* param_3)
    {
        if (commands->SelectedCommand != BtlCommand.Escape)
        {
            return _processInputsHook.OriginalFunction(commands, btl, param_3);
        }

        var input = _getPressedInput(commands);
        var canEscape = _canChangeToState(btl->Turn, BtlCommandState.Escape);

        if (input != BtlMenuInput.Enter || canEscape)
        {
            return _processInputsHook.OriginalFunction(commands, btl, param_3);
        }

        Log("Giving up battle.");
        _partyInfo->Protagonist.Hp = 0;
        btl->Turn->UsedSkill = 0;
        btl->Turn->State = BtlCommandState.Guard;
        return 5;
    }

    private delegate int ProcessInputsDelegate(BtlCommandsInfo* commands, BtlInfo* btl, void* param_3);

    private delegate BtlMenuInput GetPressedInputDelegate(BtlCommandsInfo* commands);

    private delegate bool CanChangeToStateDelegate(BtlTurnInfo* turn, BtlCommandState newState);

    private enum BtlMenuInput : short
    {
        None,
        Back,
        Enter,
        Square,
        L1,
        Up,
        Down
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct BtlCommandsInfo
    {
        [FieldOffset(4)] internal BtlCommand SelectedCommand;
    }

    private enum BtlCommand : short
    {
        Analysis,
        Tactics,
        Guard,
        Attack,
        Skill,
        Persona,
        Item,
        Escape
    }

    private enum BtlCommandState : short
    {
        Attack = 1,
        Skill = 2,
        Item = 3,
        Tactics = 4,
        Persona = 5,
        Escape = 6,
        Guard = 7,
        Analysis = 0xb
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct BtlInfo
    {
        [FieldOffset(0xcb8)] internal BtlTurnInfo* Turn;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct BtlTurnInfo
    {
        [FieldOffset(0x38)] internal UnitInfoWrapper* Unit;

        [FieldOffset(0xac)] internal BtlCommandState State;
        [FieldOffset(0xae)] internal short UsedSkill;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct UnitInfoWrapper
    {
        [FieldOffset(0xcf0)] internal UnitInfo* UnitInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct UnitInfo
    {
        [FieldOffset(8)] internal short Hp;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PartyInfo
    {
        [FieldOffset(0)]
        internal UnitInfo Protagonist;
    }

    #region Standard Overrides

    public override void ConfigurationUpdated(Config configuration)
    {
        // Apply settings from configuration.
        // ... your code here.
        _configuration = configuration;
        _logger.WriteLine($"[{_modConfig.ModId}] Config Updated: Applying");
    }

    #endregion

    #region For Exports, Serialization etc.

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Mod()
    {
    }
#pragma warning restore CS8618

    #endregion
}