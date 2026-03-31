using System;
using UnityEngine;

namespace Breathe.Gameplay
{
    // Runtime database of real constellations for the Stargaze minigame.
    // Each entry contains star positions (normalized 0-1), line connections,
    // and descriptive text for the educational reveal sequence.
    // Stored as a static class rather than ScriptableObject since the data is
    // fixed and procedurally referenced — no Inspector tuning needed.
    public static class ConstellationDatabase
    {
        [Serializable]
        public struct ConstellationData
        {
            public string Name;
            public string ScientificDescription;
            public string CharacterDescription;
            // Star positions in normalized coords (0-1 range, remapped to world space at runtime)
            public Vector2[] Stars;
            // Each pair of ints is a line connection (indices into Stars)
            public int[] LineConnections;
        }

        private static ConstellationData[] _constellations;

        public static ConstellationData[] All
        {
            get
            {
                if (_constellations == null) BuildDatabase();
                return _constellations;
            }
        }

        public static int Count => All.Length;

        public static ConstellationData Get(int index) => All[index % All.Length];

        // Shuffled selection of N constellations with no repeats
        public static ConstellationData[] GetRandomSet(int count)
        {
            var all = All;
            int[] indices = new int[all.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;

            // Fisher-Yates shuffle
            for (int i = indices.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            count = Mathf.Min(count, all.Length);
            var result = new ConstellationData[count];
            for (int i = 0; i < count; i++)
                result[i] = all[indices[i]];
            return result;
        }

        private static void BuildDatabase()
        {
            _constellations = new[]
            {
                // === ZODIAC CONSTELLATIONS ===
                new ConstellationData
                {
                    Name = "Aries",
                    ScientificDescription = "A northern constellation between Pisces and Taurus. Its brightest star, Hamal, is an orange giant 66 light-years away.",
                    CharacterDescription = "The Ram — a golden-fleeced ram from Greek mythology whose fleece was the prize of Jason and the Argonauts.",
                    Stars = new[] { V(0.3f,0.6f), V(0.4f,0.65f), V(0.55f,0.6f), V(0.65f,0.55f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3 }
                },
                new ConstellationData
                {
                    Name = "Taurus",
                    ScientificDescription = "Home to the Pleiades and Hyades star clusters and the Crab Nebula. Aldebaran, its red giant eye, is the 14th brightest star.",
                    CharacterDescription = "The Bull — Zeus disguised himself as a magnificent white bull to carry Europa across the sea to Crete.",
                    Stars = new[] { V(0.35f,0.5f), V(0.4f,0.55f), V(0.5f,0.6f), V(0.55f,0.65f), V(0.6f,0.6f), V(0.45f,0.45f), V(0.55f,0.5f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 2,4, 0,5, 5,6 }
                },
                new ConstellationData
                {
                    Name = "Gemini",
                    ScientificDescription = "Named for twin stars Castor and Pollux. Castor is actually a six-star system 51 light-years away.",
                    CharacterDescription = "The Twins — Castor and Pollux, inseparable brothers from Greek myth. Pollux was immortal; Castor was not.",
                    Stars = new[] { V(0.35f,0.7f), V(0.4f,0.6f), V(0.38f,0.5f), V(0.35f,0.4f), V(0.55f,0.72f), V(0.5f,0.6f), V(0.52f,0.5f), V(0.55f,0.38f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 4,5, 5,6, 6,7, 1,5 }
                },
                new ConstellationData
                {
                    Name = "Cancer",
                    ScientificDescription = "The faintest zodiac constellation. Contains the Beehive Cluster (M44), visible to the naked eye as a fuzzy patch.",
                    CharacterDescription = "The Crab — sent by Hera to distract Hercules during his battle with the Hydra. Small but persistent.",
                    Stars = new[] { V(0.4f,0.55f), V(0.45f,0.6f), V(0.5f,0.55f), V(0.55f,0.6f), V(0.48f,0.5f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 1,4, 2,4 }
                },
                new ConstellationData
                {
                    Name = "Leo",
                    ScientificDescription = "Regulus, its heart star, is 79 light-years away and spins so fast it bulges at its equator. Home to many galaxies.",
                    CharacterDescription = "The Lion — the Nemean Lion slain by Hercules as the first of his twelve labors, placed in the sky by Zeus.",
                    Stars = new[] { V(0.3f,0.6f), V(0.35f,0.65f), V(0.45f,0.7f), V(0.55f,0.65f), V(0.6f,0.55f), V(0.55f,0.45f), V(0.45f,0.4f), V(0.35f,0.45f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,7, 7,0 }
                },
                new ConstellationData
                {
                    Name = "Virgo",
                    ScientificDescription = "The largest zodiac constellation. Spica, its brightest star, is a blue giant 250 light-years away. Rich in galaxies.",
                    CharacterDescription = "The Maiden — often associated with Demeter, goddess of the harvest, or her daughter Persephone.",
                    Stars = new[] { V(0.5f,0.7f), V(0.45f,0.6f), V(0.5f,0.5f), V(0.55f,0.6f), V(0.4f,0.45f), V(0.6f,0.45f), V(0.5f,0.35f) },
                    LineConnections = new[] { 0,1, 0,3, 1,2, 3,2, 2,4, 2,5, 2,6 }
                },
                new ConstellationData
                {
                    Name = "Libra",
                    ScientificDescription = "The only zodiac constellation representing an inanimate object. Its stars were once considered part of Scorpius's claws.",
                    CharacterDescription = "The Scales — symbol of justice and balance, held by Astraea, the goddess of innocence and purity.",
                    Stars = new[] { V(0.4f,0.6f), V(0.5f,0.65f), V(0.6f,0.6f), V(0.45f,0.45f), V(0.55f,0.45f) },
                    LineConnections = new[] { 0,1, 1,2, 0,3, 2,4 }
                },
                new ConstellationData
                {
                    Name = "Scorpius",
                    ScientificDescription = "Antares, its red supergiant heart, is 550 light-years away and 700 times the Sun's diameter. Visible from most of Earth.",
                    CharacterDescription = "The Scorpion — sent by Gaia to slay the boastful hunter Orion. They were placed on opposite sides of the sky.",
                    Stars = new[] { V(0.3f,0.65f), V(0.35f,0.6f), V(0.4f,0.55f), V(0.45f,0.5f), V(0.5f,0.45f), V(0.55f,0.4f), V(0.6f,0.35f), V(0.65f,0.38f), V(0.7f,0.42f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,7, 7,8 }
                },
                new ConstellationData
                {
                    Name = "Sagittarius",
                    ScientificDescription = "Points toward the center of our Milky Way galaxy. Contains more Messier objects than any other constellation.",
                    CharacterDescription = "The Archer — a centaur drawing his bow, aiming at Scorpius. Often identified with Chiron, wisest of centaurs.",
                    Stars = new[] { V(0.4f,0.5f), V(0.45f,0.55f), V(0.5f,0.6f), V(0.55f,0.55f), V(0.5f,0.5f), V(0.55f,0.45f), V(0.6f,0.5f), V(0.45f,0.4f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,0, 3,5, 5,6, 4,7 }
                },
                new ConstellationData
                {
                    Name = "Capricornus",
                    ScientificDescription = "One of the faintest zodiac constellations. Its brightest star Deneb Algedi is a binary system 39 light-years away.",
                    CharacterDescription = "The Sea Goat — half goat, half fish. Associated with Pan, who leapt into the Nile and transformed to escape Typhon.",
                    Stars = new[] { V(0.35f,0.55f), V(0.4f,0.6f), V(0.5f,0.62f), V(0.6f,0.58f), V(0.65f,0.5f), V(0.55f,0.42f), V(0.4f,0.45f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,0 }
                },
                new ConstellationData
                {
                    Name = "Aquarius",
                    ScientificDescription = "Contains the Helix Nebula, the closest planetary nebula to Earth at 700 light-years. An ancient constellation recognized by many cultures.",
                    CharacterDescription = "The Water Bearer — Ganymede, a beautiful youth carried to Olympus by Zeus's eagle to serve as cupbearer to the gods.",
                    Stars = new[] { V(0.4f,0.7f), V(0.45f,0.6f), V(0.5f,0.55f), V(0.55f,0.5f), V(0.5f,0.45f), V(0.45f,0.4f), V(0.55f,0.35f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 4,6 }
                },
                new ConstellationData
                {
                    Name = "Pisces",
                    ScientificDescription = "Currently contains the vernal equinox point. A large but faint constellation spanning 889 square degrees of sky.",
                    CharacterDescription = "The Fishes — Aphrodite and Eros transformed into fish and tied themselves together to escape the monster Typhon.",
                    Stars = new[] { V(0.3f,0.5f), V(0.35f,0.55f), V(0.4f,0.5f), V(0.45f,0.55f), V(0.5f,0.6f), V(0.55f,0.55f), V(0.6f,0.5f), V(0.65f,0.55f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,7 }
                },

                // === FAMOUS CONSTELLATIONS ===
                new ConstellationData
                {
                    Name = "Orion",
                    ScientificDescription = "The most recognizable constellation. Betelgeuse (red supergiant) and Rigel (blue supergiant) mark his shoulder and foot. Contains the Orion Nebula.",
                    CharacterDescription = "The Hunter — a giant huntsman placed among the stars by Zeus. His belt of three aligned stars is the most famous asterism in the sky.",
                    Stars = new[] { V(0.35f,0.72f), V(0.6f,0.7f), V(0.4f,0.55f), V(0.45f,0.5f), V(0.5f,0.5f), V(0.55f,0.55f), V(0.38f,0.3f), V(0.58f,0.28f) },
                    LineConnections = new[] { 0,2, 2,3, 3,4, 4,5, 5,1, 2,6, 5,7, 3,4 }
                },
                new ConstellationData
                {
                    Name = "Ursa Major",
                    ScientificDescription = "The third-largest constellation. The Big Dipper asterism within it is used to find Polaris. Mizar, in the handle, is a famous double star.",
                    CharacterDescription = "The Great Bear — Callisto, transformed into a bear by jealous Hera and placed in the sky by Zeus to protect her.",
                    Stars = new[] { V(0.3f,0.55f), V(0.35f,0.6f), V(0.45f,0.62f), V(0.55f,0.6f), V(0.6f,0.55f), V(0.65f,0.5f), V(0.72f,0.52f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 3,0 }
                },
                new ConstellationData
                {
                    Name = "Ursa Minor",
                    ScientificDescription = "Contains Polaris, the current North Star, at the tip of its tail. Polaris is actually a triple star system 433 light-years away.",
                    CharacterDescription = "The Little Bear — Arcas, son of Callisto, also transformed by Zeus and set near his mother in the sky forever.",
                    Stars = new[] { V(0.5f,0.75f), V(0.48f,0.65f), V(0.45f,0.55f), V(0.5f,0.48f), V(0.55f,0.5f), V(0.58f,0.55f), V(0.52f,0.58f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,3 }
                },
                new ConstellationData
                {
                    Name = "Cassiopeia",
                    ScientificDescription = "Its distinctive W-shape is circumpolar from northern latitudes, visible all year. Contains several open star clusters and nebulae.",
                    CharacterDescription = "The Queen — a vain Ethiopian queen chained to her throne in the sky as punishment for boasting she was more beautiful than the sea nymphs.",
                    Stars = new[] { V(0.25f,0.55f), V(0.35f,0.65f), V(0.48f,0.55f), V(0.6f,0.65f), V(0.7f,0.55f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4 }
                },
                new ConstellationData
                {
                    Name = "Cygnus",
                    ScientificDescription = "The Northern Cross. Deneb, its brightest star, is one of the most luminous stars visible — a blue-white supergiant 2,600 light-years away.",
                    CharacterDescription = "The Swan — Zeus in the form of a swan, flying along the Milky Way. Also associated with the tragic musician Orpheus.",
                    Stars = new[] { V(0.5f,0.75f), V(0.5f,0.6f), V(0.5f,0.45f), V(0.35f,0.55f), V(0.65f,0.55f), V(0.5f,0.3f) },
                    LineConnections = new[] { 0,1, 1,2, 2,5, 3,1, 1,4 }
                },
                new ConstellationData
                {
                    Name = "Lyra",
                    ScientificDescription = "Small but prominent. Vega is the 5th brightest star and was the North Star 12,000 years ago. Contains the Ring Nebula (M57).",
                    CharacterDescription = "The Lyre — the harp of Orpheus, whose music could charm all living things, even stones and rivers.",
                    Stars = new[] { V(0.5f,0.72f), V(0.42f,0.58f), V(0.58f,0.58f), V(0.4f,0.45f), V(0.6f,0.45f) },
                    LineConnections = new[] { 0,1, 0,2, 1,3, 2,4, 3,4 }
                },
                new ConstellationData
                {
                    Name = "Canis Major",
                    ScientificDescription = "Home to Sirius, the brightest star in the night sky at magnitude -1.46. Sirius is only 8.6 light-years away — one of our nearest stellar neighbors.",
                    CharacterDescription = "The Greater Dog — one of Orion's hunting dogs, faithfully following the great hunter across the sky.",
                    Stars = new[] { V(0.5f,0.7f), V(0.45f,0.6f), V(0.4f,0.5f), V(0.55f,0.55f), V(0.6f,0.45f), V(0.5f,0.35f) },
                    LineConnections = new[] { 0,1, 1,2, 0,3, 3,4, 4,5, 1,3 }
                },
                new ConstellationData
                {
                    Name = "Draco",
                    ScientificDescription = "A long, winding constellation that nearly encircles Ursa Minor. Thuban was the North Star when the Egyptian pyramids were built, around 2700 BC.",
                    CharacterDescription = "The Dragon — Ladon, the hundred-headed dragon that guarded the golden apples in the Garden of the Hesperides.",
                    Stars = new[] { V(0.3f,0.5f), V(0.35f,0.55f), V(0.4f,0.6f), V(0.48f,0.58f), V(0.55f,0.62f), V(0.6f,0.55f), V(0.65f,0.5f), V(0.6f,0.45f), V(0.55f,0.42f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,7, 7,8 }
                }
            };
        }

        private static Vector2 V(float x, float y) => new Vector2(x, y);
    }
}
