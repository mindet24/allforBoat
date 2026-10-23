using NUnit.Framework;
using Trouble.Game;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>The view config's colour helpers and HUD text fallbacks.</summary>
    public class ViewConfigTests
    {
        private TrippedViewConfig _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<TrippedViewConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void Hex_ReadsRgbAndRgba()
        {
            // Arrange + Act
            var orange = TrippedViewConfig.Hex("#F2994A");
            var halfBlack = TrippedViewConfig.Hex("00000080");

            // Assert
            Assert.AreEqual(242f / 255f, orange.r, 1e-4f);
            Assert.AreEqual(153f / 255f, orange.g, 1e-4f);
            Assert.AreEqual(74f / 255f, orange.b, 1e-4f);
            Assert.AreEqual(1f, orange.a, 1e-4f);
            Assert.AreEqual(128f / 255f, halfBlack.a, 1e-4f);
        }

        [Test]
        public void Hex_BadText_IsMagenta()
        {
            // Arrange + Act
            var bad = TrippedViewConfig.Hex("#12");

            // Assert
            Assert.AreEqual(Color.magenta, bad);
        }

        [Test]
        public void CircuitColor_DefaultsAreGddColoursAndUnknownIsGrey()
        {
            // Arrange + Act
            var a = _config.CircuitColor("A");
            var d = _config.CircuitColor("D");
            var unknown = _config.CircuitColor("Z");

            // Assert
            Assert.AreEqual(TrippedViewConfig.Hex("#F2994A"), a);
            Assert.AreEqual(TrippedViewConfig.Hex("#9B51E0"), d);
            Assert.AreEqual(TrippedViewConfig.Hex("#9E9E9E"), unknown);
        }

        [Test]
        public void LoadColor_MapsBandsToGreenAmberRed()
        {
            // Arrange + Act + Assert
            Assert.AreEqual(TrippedViewConfig.Hex("#4CAF50"), _config.LoadColor(LoadBand.Green));
            Assert.AreEqual(TrippedViewConfig.Hex("#FFB300"), _config.LoadColor(LoadBand.Amber));
            Assert.AreEqual(TrippedViewConfig.Hex("#E53935"), _config.LoadColor(LoadBand.Red));
        }

        [Test]
        public void HudText_KeyInStrings_WinsOverFallback()
        {
            // Arrange
            var data = new StringsData();
            data.entries.Add(new StringEntry { key = "hud_done", text = "FINISHED" });
            var text = new HudText(new Strings(data));

            // Act + Assert
            Assert.AreEqual("FINISHED", text.Get("hud_done"));
            Assert.AreEqual("Restart (R)", text.Get("hud_restart"));
            Assert.AreEqual("CORD ×2", text.Format("hud_cords", ("n", 2)));
        }
    }
}
