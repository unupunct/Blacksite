namespace Blacksite.UI.Modules;

public static class ModuleFactory
{
    public static ModuleBase Create(string presetId) => presetId switch
    {
        "space" => new SpaceSkinModule(),
        "hacker" => new HackerSkinModule(),
        "surveillance" => new SurveillanceModule(),
        "jarvis" => new JarvisModule(),
        "lockdown" => new LockdownModule(),
        "satellite" => new SatelliteModule(),
        "reactor" => new ReactorModule(),
        "cyber" => new CyberDefenseModule(),
        "deepspace" => new DeepSpaceModule(),
        "tactical" => new MilitarySkinModule(),
        "orbital" => new OrbitalModule(),
        "quantum" => new QuantumModule(),
        "commarray" => new CommArrayModule(),
        "medical" => new MedicalModule(),
        "facility" => new FacilityModule(),
        _ => new SpaceCommandModule(),
    };
}
