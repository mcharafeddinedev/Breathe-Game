using System;
using UnityEngine;

namespace Breathe.Gameplay
{
    // Runtime database of real constellations for the Stargaze minigame.
    // Each entry contains star positions (normalized 0-1), line connections,
    // and descriptive text for the educational reveal sequence.
    // Stored as a static class rather than ScriptableObject since the data is
    // fixed and procedurally referenced — no Inspector tuning needed.
    // Science + character lines are short (one brief sentence each) for the reward caption UI.
    public static class ConstellationDatabase
    {
        // Per-star visual data: real tint color and relative brightness
        public struct StarVisual
        {
            public Color Tint;
            public float Brightness; // 0.3 (dim, mag ~4) → 1.0 (Sirius-level, mag -1.5)
            public StarVisual(float r, float g, float b, float bright)
            { Tint = new Color(r, g, b); Brightness = bright; }
        }

        [Serializable]
        public struct ConstellationData
        {
            public string Name;
            public string ScientificDescription;
            public string CharacterDescription;
            public Vector2[] Stars;
            public int[] LineConnections;
            public int Popularity;
            // Parallel to Stars — per-star color and brightness from real data
            public StarVisual[] StarVisuals;
            // Parallel to Stars — display name for labeled stars (null/empty = unlabeled)
            public string[] StarNames;
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

        // Weighted selection: popular constellations are more likely to appear first
        public static ConstellationData[] GetRandomSet(int count)
        {
            var all = All;
            count = Mathf.Min(count, all.Length);

            var pool = new System.Collections.Generic.List<int>(all.Length);
            for (int i = 0; i < all.Length; i++) pool.Add(i);

            var result = new ConstellationData[count];
            for (int picked = 0; picked < count; picked++)
            {
                float totalWeight = 0f;
                for (int i = 0; i < pool.Count; i++)
                    totalWeight += Mathf.Max(1, all[pool[i]].Popularity);

                float roll = UnityEngine.Random.Range(0f, totalWeight);
                float running = 0f;
                int chosen = 0;
                for (int i = 0; i < pool.Count; i++)
                {
                    running += Mathf.Max(1, all[pool[i]].Popularity);
                    if (roll <= running) { chosen = i; break; }
                }

                result[picked] = all[pool[chosen]];
                pool.RemoveAt(chosen);
            }
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
                    ScientificDescription = "Aries is a small zodiac patch; its brightest star, Hamal, is an orange giant.",
                    CharacterDescription = "In myth, the Ram belongs to the tale of the Golden Fleece.",
                    Stars = new[] { V(0.3f,0.6f), V(0.4f,0.65f), V(0.55f,0.6f), V(0.65f,0.55f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3 },
                    Popularity = 1,
                    StarVisuals = new[] { S(1f,.97f,.95f, .36f), S(1f,.97f,.95f, .48f), S(1f,.72f,.42f, .55f), S(1f,.97f,.95f, .38f) },
                    StarNames = new[] { "Mesarthim", "Sheratan", "Hamal", "" }
                },
                new ConstellationData
                {
                    Name = "Taurus",
                    ScientificDescription = "Taurus hosts the Hyades, the Pleiades, and the bright orange star Aldebaran.",
                    CharacterDescription = "The Bull appears in the story of Zeus carrying Europa across the sea.",
                    Stars = new[] { V(0.35f,0.5f), V(0.4f,0.55f), V(0.5f,0.6f), V(0.55f,0.65f), V(0.6f,0.6f), V(0.45f,0.45f), V(0.55f,0.5f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 2,4, 0,5, 5,6 },
                    Popularity = 2,
                    StarVisuals = new[] { S(.68f,.82f,1f, .42f), S(1f,.72f,.42f, .37f), S(1f,.55f,.25f, .82f), S(1f,.97f,.95f, .38f), S(1f,.72f,.42f, .36f), S(.68f,.82f,1f, .62f), S(1f,.97f,.95f, .35f) },
                    StarNames = new[] { "", "Ain", "Aldebaran", "", "", "Elnath", "" }
                },
                new ConstellationData
                {
                    Name = "Gemini",
                    ScientificDescription = "Gemini is named for its twin head stars, Castor and Pollux — bright and easy to spot.",
                    CharacterDescription = "The Twins are famous brothers who sail together in Greek legend.",
                    Stars = new[] { V(0.35f,0.7f), V(0.4f,0.6f), V(0.38f,0.5f), V(0.35f,0.4f), V(0.55f,0.72f), V(0.5f,0.6f), V(0.52f,0.5f), V(0.55f,0.38f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 4,5, 5,6, 6,7, 1,5 },
                    Popularity = 3,
                    StarVisuals = new[] { S(.68f,.82f,1f, .62f), S(1f,.97f,.95f, .40f), S(1f,.97f,.95f, .38f), S(1f,.97f,.95f, .36f), S(1f,.72f,.42f, .70f), S(1f,.97f,.95f, .40f), S(1f,.97f,.95f, .38f), S(1f,.97f,.95f, .36f) },
                    StarNames = new[] { "Castor", "", "", "", "Pollux", "", "", "" }
                },
                new ConstellationData
                {
                    Name = "Cancer",
                    ScientificDescription = "Cancer is a faint zodiac group but contains the open cluster called the Beehive.",
                    CharacterDescription = "The Crab nips at the hero Hercules in old myths of his labors.",
                    Stars = new[] { V(0.4f,0.55f), V(0.45f,0.6f), V(0.5f,0.55f), V(0.55f,0.6f), V(0.48f,0.5f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 1,4, 2,4 },
                    Popularity = 1,
                    StarVisuals = new[] { S(1f,.94f,.76f, .37f), S(1f,.94f,.76f, .36f), S(1f,.94f,.76f, .35f), S(1f,.94f,.76f, .36f), S(1f,.94f,.76f, .34f) },
                    StarNames = new[] { "Acubens", "Asellus Borealis", "", "Asellus Australis", "" }
                },
                new ConstellationData
                {
                    Name = "Leo",
                    ScientificDescription = "Leo is easy to see; its brightest star, Regulus, sits at the lion’s front.",
                    CharacterDescription = "The Lion recalls the beast Hercules met in his first famous labor.",
                    Stars = new[] { V(0.3f,0.6f), V(0.35f,0.65f), V(0.45f,0.7f), V(0.55f,0.65f), V(0.6f,0.55f), V(0.55f,0.45f), V(0.45f,0.4f), V(0.35f,0.45f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,7, 7,0 },
                    Popularity = 3,
                    StarVisuals = new[] { S(.68f,.82f,1f, .68f), S(1f,.97f,.95f, .37f), S(1f,.72f,.42f, .55f), S(1f,.97f,.95f, .54f), S(1f,.94f,.76f, .40f), S(1f,.97f,.95f, .42f), S(1f,.97f,.95f, .38f), S(1f,.94f,.76f, .40f) },
                    StarNames = new[] { "Regulus", "", "Algieba", "Denebola", "", "", "", "" }
                },
                new ConstellationData
                {
                    Name = "Virgo",
                    ScientificDescription = "Virgo is the largest zodiac figure; Spica is its bright blue guide star.",
                    CharacterDescription = "The Maiden is tied in myth to the harvest and the changing seasons.",
                    Stars = new[] { V(0.5f,0.7f), V(0.45f,0.6f), V(0.5f,0.5f), V(0.55f,0.6f), V(0.4f,0.45f), V(0.6f,0.45f), V(0.5f,0.35f) },
                    LineConnections = new[] { 0,1, 0,3, 1,2, 3,2, 2,4, 2,5, 2,6 },
                    Popularity = 2,
                    StarVisuals = new[] { S(1f,.94f,.76f, .38f), S(1f,.97f,.95f, .46f), S(.68f,.82f,1f, .78f), S(1f,.94f,.76f, .44f), S(1f,.94f,.76f, .36f), S(1f,.94f,.76f, .36f), S(1f,.94f,.76f, .35f) },
                    StarNames = new[] { "", "Porrima", "Spica", "Vindemiatrix", "", "", "" }
                },
                new ConstellationData
                {
                    Name = "Libra",
                    ScientificDescription = "Libra is the only zodiac sign drawn as a balance, with two bright scale stars.",
                    CharacterDescription = "The Scales stand for fairness; older sky maps showed them on the Scorpion’s claws.",
                    Stars = new[] { V(0.4f,0.6f), V(0.5f,0.65f), V(0.6f,0.6f), V(0.45f,0.45f), V(0.55f,0.45f) },
                    LineConnections = new[] { 0,1, 1,2, 0,3, 2,4 },
                    Popularity = 1,
                    StarVisuals = new[] { S(1f,.97f,.95f, .46f), S(.68f,.85f,1f, .48f), S(1f,.97f,.95f, .40f), S(1f,.94f,.76f, .37f), S(1f,.94f,.76f, .37f) },
                    StarNames = new[] { "Zubenelgenubi", "Zubeneschamali", "", "", "" }
                },
                new ConstellationData
                {
                    Name = "Scorpius",
                    ScientificDescription = "Scorpius is ruled by the red supergiant Antares, a rival in color to the planet Mars.",
                    CharacterDescription = "The Scorpion and Orion are kept on opposite sides of the sky in legend.",
                    Stars = new[] { V(0.3f,0.65f), V(0.35f,0.6f), V(0.4f,0.55f), V(0.45f,0.5f), V(0.5f,0.45f), V(0.55f,0.4f), V(0.6f,0.35f), V(0.65f,0.38f), V(0.7f,0.42f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,7, 7,8 },
                    Popularity = 3,
                    StarVisuals = new[] { S(.68f,.82f,1f, .50f), S(.68f,.82f,1f, .44f), S(.68f,.82f,1f, .50f), S(1f,.42f,.22f, .78f), S(.68f,.82f,1f, .42f), S(.68f,.82f,1f, .42f), S(.68f,.82f,1f, .44f), S(.68f,.82f,1f, .40f), S(.75f,.85f,1f, .44f) },
                    StarNames = new[] { "Dschubba", "", "", "Antares", "", "", "Shaula", "", "" }
                },
                new ConstellationData
                {
                    Name = "Sagittarius",
                    ScientificDescription = "Sagittarius points toward the Milky Way’s center, full of nebulae and young stars.",
                    CharacterDescription = "The Archer is drawn as a centaur with a ready bow in classical art.",
                    Stars = new[] { V(0.4f,0.5f), V(0.45f,0.55f), V(0.5f,0.6f), V(0.55f,0.55f), V(0.5f,0.5f), V(0.55f,0.45f), V(0.6f,0.5f), V(0.45f,0.4f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,0, 3,5, 5,6, 4,7 },
                    Popularity = 2,
                    StarVisuals = new[] { S(.68f,.82f,1f, .58f), S(1f,.72f,.42f, .46f), S(.68f,.82f,1f, .55f), S(1f,.97f,.95f, .48f), S(1f,.97f,.95f, .44f), S(1f,.97f,.95f, .40f), S(1f,.94f,.76f, .42f), S(1f,.94f,.76f, .38f) },
                    StarNames = new[] { "Kaus Australis", "", "Nunki", "Ascella", "", "", "", "" }
                },
                new ConstellationData
                {
                    Name = "Capricornus",
                    ScientificDescription = "Capricornus is a faint, ancient figure; Deneb Algedi is its brightest star.",
                    CharacterDescription = "The Sea-Goat blends goat and fish, including tales of the god Pan.",
                    Stars = new[] { V(0.35f,0.55f), V(0.4f,0.6f), V(0.5f,0.62f), V(0.6f,0.58f), V(0.65f,0.5f), V(0.55f,0.42f), V(0.4f,0.45f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,0 },
                    Popularity = 1,
                    StarVisuals = new[] { S(1f,.94f,.76f, .40f), S(1f,.94f,.76f, .38f), S(1f,.94f,.76f, .36f), S(1f,.97f,.95f, .44f), S(1f,.97f,.95f, .44f), S(1f,.94f,.76f, .36f), S(1f,.94f,.76f, .37f) },
                    StarNames = new[] { "Algedi", "", "", "Deneb Algedi", "Nashira", "", "" }
                },
                new ConstellationData
                {
                    Name = "Aquarius",
                    ScientificDescription = "Aquarius holds bright stars in a part of the sky rich with star clusters and nebulae.",
                    CharacterDescription = "The Water Bearer is classically shown pouring a stream toward the sea.",
                    Stars = new[] { V(0.4f,0.7f), V(0.45f,0.6f), V(0.5f,0.55f), V(0.55f,0.5f), V(0.5f,0.45f), V(0.45f,0.4f), V(0.55f,0.35f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 4,6 },
                    Popularity = 1,
                    StarVisuals = new[] { S(1f,.94f,.76f, .43f), S(1f,.94f,.76f, .44f), S(1f,.94f,.76f, .37f), S(1f,.94f,.76f, .36f), S(1f,.97f,.95f, .40f), S(1f,.94f,.76f, .36f), S(1f,.94f,.76f, .35f) },
                    StarNames = new[] { "Sadalmelik", "Sadalsuud", "", "", "Skat", "", "" }
                },
                new ConstellationData
                {
                    Name = "Pisces",
                    ScientificDescription = "Pisces is large, faint, and lies along the path the Sun takes through the year.",
                    CharacterDescription = "The Fishes appear in stories of gods who turned into fish to escape a monster.",
                    Stars = new[] { V(0.3f,0.5f), V(0.35f,0.55f), V(0.4f,0.5f), V(0.45f,0.55f), V(0.5f,0.6f), V(0.55f,0.55f), V(0.6f,0.5f), V(0.65f,0.55f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,7 },
                    Popularity = 1,
                    StarVisuals = new[] { S(1f,.97f,.95f, .36f), S(1f,.97f,.95f, .35f), S(1f,.97f,.95f, .36f), S(1f,.97f,.95f, .35f), S(1f,.94f,.76f, .37f), S(1f,.97f,.95f, .35f), S(1f,.97f,.95f, .36f), S(1f,.97f,.95f, .35f) },
                    StarNames = new[] { "", "", "", "Alrescha", "", "", "", "" }
                },

                // === FAMOUS CONSTELLATIONS ===
                new ConstellationData
                {
                    Name = "Orion",
                    ScientificDescription = "Orion is one of the easiest constellations, with red Betelgeuse, blue Rigel, and a straight belt of three stars.",
                    CharacterDescription = "The Hunter is a famous sky figure whose belt has guided travelers for ages.",
                    Stars = new[] { V(0.35f,0.72f), V(0.6f,0.7f), V(0.4f,0.55f), V(0.45f,0.5f), V(0.5f,0.5f), V(0.55f,0.55f), V(0.38f,0.3f), V(0.58f,0.28f) },
                    LineConnections = new[] { 0,2, 2,3, 3,4, 4,5, 5,1, 2,6, 5,7, 3,4 },
                    Popularity = 3,
                    StarVisuals = new[] { S(1f,.42f,.22f, .88f), S(.68f,.82f,1f, .62f), S(.68f,.82f,1f, .52f), S(.68f,.82f,1f, .56f), S(.68f,.82f,1f, .58f), S(.68f,.82f,1f, .52f), S(.68f,.82f,1f, .54f), S(.62f,.78f,1f, .92f) },
                    StarNames = new[] { "Betelgeuse", "Bellatrix", "", "Mintaka", "Alnilam", "", "Saiph", "Rigel" }
                },
                new ConstellationData
                {
                    Name = "Ursa Major",
                    ScientificDescription = "Ursa Major is huge; the Big Dipper is part of it, and the pointers lead toward Polaris, the North Star.",
                    CharacterDescription = "The Great Bear is tied to the myth of Callisto among the constellations.",
                    Stars = new[] { V(0.3f,0.55f), V(0.35f,0.6f), V(0.45f,0.62f), V(0.55f,0.6f), V(0.6f,0.55f), V(0.65f,0.5f), V(0.72f,0.52f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 3,0 },
                    Popularity = 3,
                    StarVisuals = new[] { S(1f,.72f,.42f, .58f), S(1f,.97f,.95f, .50f), S(1f,.97f,.95f, .48f), S(1f,.97f,.95f, .40f), S(1f,.97f,.95f, .60f), S(1f,.97f,.95f, .55f), S(.68f,.82f,1f, .58f) },
                    StarNames = new[] { "Dubhe", "Merak", "Phecda", "Megrez", "Alioth", "Mizar", "Alkaid" }
                },
                new ConstellationData
                {
                    Name = "Ursa Minor",
                    ScientificDescription = "Ursa Minor is the Little Bear; Polaris caps its tail and marks the north celestial pole.",
                    CharacterDescription = "The Little Bear is paired in myth with the Great Bear in the same story family.",
                    Stars = new[] { V(0.5f,0.75f), V(0.48f,0.65f), V(0.45f,0.55f), V(0.5f,0.48f), V(0.55f,0.5f), V(0.58f,0.55f), V(0.52f,0.58f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,3 },
                    Popularity = 2,
                    StarVisuals = new[] { S(1f,.94f,.76f, .55f), S(1f,.72f,.42f, .54f), S(1f,.97f,.95f, .42f), S(1f,.94f,.76f, .36f), S(1f,.94f,.76f, .35f), S(1f,.94f,.76f, .34f), S(1f,.94f,.76f, .35f) },
                    StarNames = new[] { "Polaris", "Kochab", "Pherkad", "", "", "", "" }
                },
                new ConstellationData
                {
                    Name = "Cassiopeia",
                    ScientificDescription = "Cassiopeia’s bright W rides the Milky Way and is well placed for most northern evenings.",
                    CharacterDescription = "The Queen is remembered in sky lore for her pride and the sea’s answer.",
                    Stars = new[] { V(0.25f,0.55f), V(0.35f,0.65f), V(0.48f,0.55f), V(0.6f,0.65f), V(0.7f,0.55f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4 },
                    Popularity = 3,
                    StarVisuals = new[] { S(.68f,.82f,1f, .38f), S(1f,.97f,.95f, .46f), S(.68f,.82f,1f, .48f), S(1f,.72f,.42f, .52f), S(1f,.97f,.95f, .50f) },
                    StarNames = new[] { "Segin", "Ruchbah", "Navi", "Schedar", "Caph" }
                },
                new ConstellationData
                {
                    Name = "Cygnus",
                    ScientificDescription = "Cygnus is also called the Northern Cross, with Deneb as a bright, distant beacon.",
                    CharacterDescription = "The Swan glides along the Milky Way in several classical stories.",
                    Stars = new[] { V(0.5f,0.75f), V(0.5f,0.6f), V(0.5f,0.45f), V(0.35f,0.55f), V(0.65f,0.55f), V(0.5f,0.3f) },
                    LineConnections = new[] { 0,1, 1,2, 2,5, 3,1, 1,4 },
                    Popularity = 2,
                    StarVisuals = new[] { S(.68f,.82f,1f, .68f), S(1f,.94f,.76f, .52f), S(1f,.72f,.42f, .48f), S(.68f,.82f,1f, .48f), S(.68f,.82f,1f, .44f), S(1f,.72f,.42f, .42f) },
                    StarNames = new[] { "Deneb", "Sadr", "", "Gienah", "", "Albireo" }
                },
                new ConstellationData
                {
                    Name = "Lyra",
                    ScientificDescription = "Lyra is small and dominated by Vega, one of the night sky’s brightest stars.",
                    CharacterDescription = "The Lyre is the little harp of Orpheus in legend.",
                    Stars = new[] { V(0.5f,0.72f), V(0.42f,0.58f), V(0.58f,0.58f), V(0.4f,0.45f), V(0.6f,0.45f) },
                    LineConnections = new[] { 0,1, 0,2, 1,3, 2,4, 3,4 },
                    Popularity = 2,
                    StarVisuals = new[] { S(.68f,.82f,1f, .90f), S(.68f,.82f,1f, .37f), S(.68f,.82f,1f, .40f), S(.68f,.82f,1f, .36f), S(.68f,.82f,1f, .34f) },
                    StarNames = new[] { "Vega", "Sheliak", "Sulafat", "", "" }
                },
                new ConstellationData
                {
                    Name = "Canis Major",
                    ScientificDescription = "Canis Major holds Sirius, the single brightest star in Earth’s night sky and a close neighbor in space.",
                    CharacterDescription = "The Greater Dog follows Orion as his bright hunting companion.",
                    Stars = new[] { V(0.5f,0.7f), V(0.45f,0.6f), V(0.4f,0.5f), V(0.55f,0.55f), V(0.6f,0.45f), V(0.5f,0.35f) },
                    LineConnections = new[] { 0,1, 1,2, 0,3, 3,4, 4,5, 1,3 },
                    Popularity = 2,
                    StarVisuals = new[] { S(.85f,.9f,1f, 1f), S(.68f,.82f,1f, .55f), S(1f,.94f,.76f, .38f), S(1f,.94f,.76f, .58f), S(.68f,.82f,1f, .62f), S(.68f,.82f,1f, .48f) },
                    StarNames = new[] { "Sirius", "Mirzam", "", "Wezen", "Adhara", "Aludra" }
                },
                new ConstellationData
                {
                    Name = "Draco",
                    ScientificDescription = "Draco is a long chain around the north sky; Thuban once served as the pole star in antiquity.",
                    CharacterDescription = "The Dragon appears in old myths as a fearsome, many-headed sky guardian.",
                    Stars = new[] { V(0.3f,0.5f), V(0.35f,0.55f), V(0.4f,0.6f), V(0.48f,0.58f), V(0.55f,0.62f), V(0.6f,0.55f), V(0.65f,0.5f), V(0.6f,0.45f), V(0.55f,0.42f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,7, 7,8 },
                    Popularity = 1,
                    StarVisuals = new[] { S(1f,.72f,.42f, .52f), S(1f,.94f,.76f, .44f), S(1f,.97f,.95f, .37f), S(1f,.94f,.76f, .38f), S(1f,.94f,.76f, .36f), S(1f,.94f,.76f, .38f), S(1f,.94f,.76f, .36f), S(1f,.94f,.76f, .35f), S(1f,.94f,.76f, .36f) },
                    StarNames = new[] { "Eltanin", "Rastaban", "Thuban", "", "", "", "", "", "" }
                }
            };
        }

        private static Vector2 V(float x, float y) => new Vector2(x, y);
        private static StarVisual S(float r, float g, float b, float bright) => new StarVisual(r, g, b, bright);
    }
}
