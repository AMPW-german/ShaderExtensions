using HarmonyLib;
using StarMap.API;

namespace ShaderExtensions
{
    [StarMapMod]
    public class ShaderExtensions
    {
        [StarMapBeforeMain]
        public void Setup()
        {
            var harmony = new Harmony("ShaderExtensions");
            harmony.PatchAll();
        }
    }
}
