using ImGuiNET;
using System.Text;

namespace craftsim;

public class RecommendationUI
{
    private GameCraftState _game;
    private Solver _solver = new();
    public CraftAction Recommendation { get; private set; }
    public string RecommendationComment { get; private set; } = "";

    public RecommendationUI(GameCraftState game)
    {
        _game = game;
    }

    public unsafe void Draw()
    {
        if (_game.CurState == null || _game.CurStep == null)
        {
            ImGui.TextUnformatted("Craft not running!");
            return;
        }

        var sim = new Simulator(_game.CurState, 0);
        (Recommendation, RecommendationComment) = _solver.SolveNextStep(sim, _game.CurStep);

        ImGui.TextUnformatted($"Stats: {_game.CurState.StatCraftsmanship}/{_game.CurState.StatControl}/{_game.CurState.StatCP} (L{_game.CurState.StatLevel}){(_game.CurState.Specialist ? " (spec)" : "")}{(_game.CurState.Splendorous ? " (splend)" : "")}");
        ImGui.TextUnformatted($"Craft: L{_game.CurState.CraftLevel}{(_game.CurState.CraftExpert ? "X" : "")} {_game.CurState.CraftDurability}dur, {_game.CurState.CraftProgress}p, {_game.CurState.CraftQualityMax}q ({_game.CurState.CraftQualityMin1}/{_game.CurState.CraftQualityMin2}/{_game.CurState.CraftQualityMin3})");
        ImGui.TextUnformatted($"Base progress: {sim.BaseProgress()} ({_game.CurState.CraftProgress * 100.0 / sim.BaseProgress():f2}p), base quality: {sim.BaseQuality()} ({_game.CurState.CraftQualityMax * 100.0 / sim.BaseQuality():f2}p)");
        ImGui.Separator();

        DrawProgress("Progress", _game.CurStep.Progress, _game.CurState.CraftProgress);
        DrawProgress("Quality", _game.CurStep.Quality, _game.CurState.CraftQualityMax);
        DrawProgress("Durability", _game.CurStep.Durability, _game.CurState.CraftDurability);
        DrawProgress("CP", _game.CurStep.RemainingCP, _game.CurState.StatCP);

        var sb = new StringBuilder($"{_game.CurStep.Condition}; IQ:{_game.CurStep.IQStacks}");
        AddBuff(sb, "WN", _game.CurStep.WasteNotLeft);
        AddBuff(sb, "Manip", _game.CurStep.ManipulationLeft);
        AddBuff(sb, "GS", _game.CurStep.GreatStridesLeft);
        AddBuff(sb, "Inno", _game.CurStep.InnovationLeft);
        AddBuff(sb, "Vene", _game.CurStep.VenerationLeft);
        AddBuff(sb, "MuMe", _game.CurStep.MuscleMemoryLeft);
        AddBuff(sb, "FA", _game.CurStep.FinalAppraisalLeft);
        if (_game.CurStep.HeartAndSoulActive)
            sb.Append(" HnS");
        ImGui.TextUnformatted(sb.ToString());

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Solver setup"))
            _solver.Draw();

        if (ImGui.Button($"Recommendation: {Recommendation} ({RecommendationComment})"))
            _game.UseAction(Recommendation);
    }

    private void DrawProgress(string label, int value, int max)
    {
        ImGui.ProgressBar((float)value / max, new(100, 0), $"{value * 100.0 / max:f2}% ({value}/{max})");
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
    }

    private void AddBuff(StringBuilder sb, string mnem, int left)
    {
        if (left > 0)
            sb.Append($" {mnem}:{left}");
    }
}
