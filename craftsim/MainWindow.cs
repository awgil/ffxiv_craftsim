using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;

namespace craftsim;

public unsafe class MainWindow : Window
{
    private TransitionDB _trans = new();
    private SimulatorUI _sim;
    private SolverUI _solver;

    public MainWindow() : base("CraftSim")
    {
        // TODO: customization...
        bool hqJhinga = true;
        bool hqCPDraught = true;
        int fcCraftsmanship = 0;
        int fcControl = 0;
        var craft = new CraftState
        {
            StatCraftsmanship = 4065 + 5 * fcCraftsmanship,
            StatControl = 3964 + 5 * fcControl + (hqJhinga ? 90 : 0),
            StatCP = 616 + (hqJhinga ? 86 : 0) + (hqCPDraught ? 21 : 0),
            StatLevel = 90,
            Specialist = true,
            Splendorous = true,
            CraftExpert = true,
            CraftLevel = 90,
            CraftDurability = 60,
            CraftProgress = 6600,
            CraftProgressDivider = 180,
            CraftProgressModifier = 100,
            CraftQualityDivider = 180,
            CraftQualityModifier = 100,
            CraftQualityMax = 15368,
            CraftQualityMin1 = 7500,
            CraftQualityMin2 = 11250,
            CraftQualityMin3 = 15000,
            CraftConditionProbabilities = CraftState.EWRelicT1CraftConditionProbabilities()
        };
        _sim = new(craft);
        _solver = new(craft);
    }

    public override void Draw()
    {
        try
        {
            _trans.Update();

            using var tabs = ImRaii.TabBar("Tabs");
            if (tabs)
            {
                using (var tab = ImRaii.TabItem("Sim"))
                    if (tab)
                        _sim.Draw();
                using (var tab = ImRaii.TabItem("Solver stats"))
                    if (tab)
                        _solver.Draw();
                using (var tab = ImRaii.TabItem("Transitions"))
                    if (tab)
                        _trans.Draw();
            }
        }
        catch (Exception ex)
        {
            Service.Log.Error($"Error: {ex.Message}");
        }
    }
}
