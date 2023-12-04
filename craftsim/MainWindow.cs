using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;

namespace craftsim;

public unsafe class MainWindow : Window, IDisposable
{
    private GameCraftState _gameState = new();
    private TransitionDB _trans;
    private SimulatorUI _sim;
    private SolverUI _solver;
    private RecommendationUI _recommendations;

    public MainWindow() : base("CraftSim")
    {
        _trans = new(_gameState);
        _recommendations = new(_gameState);

        // TODO: customization...
        bool hqJhinga = true;
        bool hqCPDraught = true;
        int fcCraftsmanship = 0;
        int fcControl = 0;
        // T1 relic
        //var craft = new CraftState
        //{
        //    StatCraftsmanship = 4065 + 5 * fcCraftsmanship,
        //    StatControl = 3964 + 5 * fcControl + (hqJhinga ? 90 : 0),
        //    StatCP = 616 + (hqJhinga ? 86 : 0) + (hqCPDraught ? 21 : 0),
        //    StatLevel = 90,
        //    Specialist = true,
        //    Splendorous = true,
        //    CraftExpert = true,
        //    CraftLevel = 90,
        //    CraftDurability = 60,
        //    CraftProgress = 6600,
        //    CraftProgressDivider = 180,
        //    CraftProgressModifier = 100,
        //    CraftQualityDivider = 180,
        //    CraftQualityModifier = 100,
        //    CraftQualityMax = 15368,
        //    CraftQualityMin1 = 7500,
        //    CraftQualityMin2 = 11250,
        //    CraftQualityMin3 = 15000,
        //    CraftConditionProbabilities = CraftState.EWRelicT2CraftConditionProbabilities()
        //};
        // T2 relic
        //var craft = new CraftState
        //{
        //    StatCraftsmanship = 4077 + 5 * fcCraftsmanship,
        //    StatControl = 3972 + 5 * fcControl + (hqJhinga ? 90 : 0),
        //    StatCP = 616 + (hqJhinga ? 86 : 0) + (hqCPDraught ? 21 : 0),
        //    StatLevel = 90,
        //    Specialist = true,
        //    Splendorous = true,
        //    CraftExpert = true,
        //    CraftLevel = 90,
        //    CraftDurability = 60,
        //    CraftProgress = 4400 * 160 / 100,
        //    CraftProgressDivider = 180,
        //    CraftProgressModifier = 100,
        //    CraftQualityDivider = 180,
        //    CraftQualityModifier = 100,
        //    CraftQualityMax = 9060 * 180 / 100,
        //    CraftQualityMin1 = 8000,
        //    CraftQualityMin2 = 12000,
        //    CraftQualityMin3 = 16000,
        //    CraftConditionProbabilities = CraftState.EWRelicT2CraftConditionProbabilities()
        //};
        // 5-star
        var craft = new CraftState
        {
            StatCraftsmanship = 4088 + 5 * fcCraftsmanship,
            StatControl = 3981 + 5 * fcControl + (hqJhinga ? 90 : 0),
            StatCP = 616 + (hqJhinga ? 86 : 0) + (hqCPDraught ? 21 : 0),
            StatLevel = 90,
            Specialist = true,
            Splendorous = true,
            CraftExpert = true,
            CraftLevel = 90,
            CraftDurability = 70,
            CraftProgress = 4400 * 210 / 100,
            CraftProgressDivider = 180,
            CraftProgressModifier = 100,
            CraftQualityDivider = 180,
            CraftQualityModifier = 100,
            CraftQualityMax = 9080 * 221 / 100,
            CraftQualityMin1 = 20000,
            CraftQualityMin2 = 20000,
            CraftQualityMin3 = 20000,
            CraftConditionProbabilities = CraftState.EW5StarCraftConditionProbabilities()
        };
        _sim = new(craft);
        _solver = new(craft);
    }

    public void Dispose()
    {
        _gameState.Dispose();
    }

    public override void Draw()
    {
        try
        {
            _gameState.Update();
            _trans.Update();

            using var tabs = ImRaii.TabBar("Tabs");
            if (tabs)
            {
                using (var tab = ImRaii.TabItem("Assist"))
                    if (tab)
                        _recommendations.Draw();
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
            Service.Log.Error($"Error: {ex}");
        }
    }

    public void UseRecommendedAction() => _gameState.UseAction(_recommendations.Recommendation);
}
