using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace RayMarcher.Framework;

public sealed class FrameScheduler
{
    private readonly Action<float>[] _ticks;
    private readonly string[] _names;
    private readonly Phase[] _phases;
    private readonly SetupEntry[] _setups;
    private bool _setupRan;

    public FrameScheduler(IServiceProvider provider, FrameSystemCatalog catalog)
    {
        var ticks  = new List<TickEntry>();
        var setups = new List<SetupEntry>();
        var errors = new List<string>();

        foreach (var type in catalog.Types)
        {
            try
            {
                var bindings = FrameSystemDiscovery.Collect(provider.GetRequiredService(type));
                ticks.AddRange(bindings.Ticks);
                setups.AddRange(bindings.Setups);
            }
            catch (Exception ex)
            {
                errors.Add($"  {type.Name}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Frame system configuration errors:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors));

        var ordered = ticks
            .OrderBy(e => (int)e.Phase)
            .ThenBy(e => e.Order)
            .ThenBy(e => e.Name, StringComparer.Ordinal)
            .ToArray();

        _ticks  = ordered.Select(e => e.Tick).ToArray();
        _names  = ordered.Select(e => e.Name).ToArray();
        _phases = ordered.Select(e => e.Phase).ToArray();

        _setups = setups
            .OrderBy(e => (int)e.Phase)
            .ThenBy(e => e.Order)
            .ThenBy(e => e.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public int Count => _ticks.Length;
    
    public void RunSetup()
    {
        if (_setupRan)
            throw new InvalidOperationException("FrameScheduler.RunSetup() has already been called.");
        _setupRan = true;

        foreach (var s in _setups)
        {
            try
            {
                s.Setup();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Setup failed in {s.Name}: {ex.Message}", ex);
            }
        }
    }

    public string DumpSetupOrder()
    {
        var sb = new StringBuilder($"Setup order ({_setups.Length} methods):");
        SetupPhase? last = null;

        for (int i = 0; i < _setups.Length; i++)
        {
            if (_setups[i].Phase != last)
            {
                sb.AppendLine().Append($"[{_setups[i].Phase}]");
                last = _setups[i].Phase;
            }
            sb.AppendLine().Append($"  {i,2}. {_setups[i].Name}");
        }
        return sb.ToString();
    }

    public void Tick(float dt)
    {
        for (int i = 0; i < _ticks.Length; i++)
            _ticks[i](dt);
    }
    
    public string DumpOrder()
    {
        var sb = new StringBuilder($"Frame order ({_ticks.Length} systems):");
        Phase? last = null;

        for (int i = 0; i < _names.Length; i++)
        {
            if (_phases[i] != last)
            {
                sb.AppendLine().Append($"[{_phases[i]}]");
                last = _phases[i];
            }
            sb.AppendLine().Append($"  {i,2}. {_names[i]}");
        }
        return sb.ToString();
    }
}
