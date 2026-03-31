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
                    ScientificDescription = "A small zodiac constellation between Pisces (PIE-seez) and Taurus. Its brightest star Hamal (hah-MAHL) glows orange, about 66 light-years away.",
                    CharacterDescription = "The Ram — famous for the golden fleece that Jason and the Argonauts set out on an epic quest to find.",
                    Stars = new[] { V(0.3f,0.6f), V(0.4f,0.65f), V(0.55f,0.6f), V(0.65f,0.55f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3 },
                    Popularity = 1,
                    StarVisuals = new[] { S(1f,.97f,.95f, .36f), S(1f,.97f,.95f, .48f), S(1f,.72f,.42f, .55f), S(1f,.97f,.95f, .38f) },
                    StarNames = new[] { "Mesarthim", "Sheratan", "Hamal", "" }
                },
                new ConstellationData
                {
                    Name = "Taurus",
                    ScientificDescription = "Home to the Pleiades (PLEE-uh-deez) and Hyades (HYE-uh-deez) star clusters, plus the Crab Nebula. Its bright orange eye is Aldebaran (al-DEB-uh-ran), a red giant about 65 light-years away.",
                    CharacterDescription = "The Bull — Zeus disguised himself as a white bull to carry Europa (yoo-ROH-puh) across the sea to Crete.",
                    Stars = new[] { V(0.35f,0.5f), V(0.4f,0.55f), V(0.5f,0.6f), V(0.55f,0.65f), V(0.6f,0.6f), V(0.45f,0.45f), V(0.55f,0.5f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 2,4, 0,5, 5,6 },
                    Popularity = 2,
                    StarVisuals = new[] { S(.68f,.82f,1f, .42f), S(1f,.72f,.42f, .37f), S(1f,.55f,.25f, .82f), S(1f,.97f,.95f, .38f), S(1f,.72f,.42f, .36f), S(.68f,.82f,1f, .62f), S(1f,.97f,.95f, .35f) },
                    StarNames = new[] { "", "Ain", "Aldebaran", "", "", "Elnath", "" }
                },
                new ConstellationData
                {
                    Name = "Gemini",
                    ScientificDescription = "Two bright stars mark the heads of the twins. Fun fact: Castor is actually six stars orbiting each other, about 51 light-years away.",
                    CharacterDescription = "The Twins — mythic brothers and patrons of sailors who stuck together even though only Pollux was immortal.",
                    Stars = new[] { V(0.35f,0.7f), V(0.4f,0.6f), V(0.38f,0.5f), V(0.35f,0.4f), V(0.55f,0.72f), V(0.5f,0.6f), V(0.52f,0.5f), V(0.55f,0.38f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 4,5, 5,6, 6,7, 1,5 },
                    Popularity = 3,
                    StarVisuals = new[] { S(.68f,.82f,1f, .62f), S(1f,.97f,.95f, .40f), S(1f,.97f,.95f, .38f), S(1f,.97f,.95f, .36f), S(1f,.72f,.42f, .70f), S(1f,.97f,.95f, .40f), S(1f,.97f,.95f, .38f), S(1f,.97f,.95f, .36f) },
                    StarNames = new[] { "Castor", "", "", "", "Pollux", "", "", "" }
                },
                new ConstellationData
                {
                    Name = "Cancer",
                    ScientificDescription = "The dimmest zodiac constellation, but it hides a gem: the Beehive Cluster, a fuzzy cloud of stars you can spot without a telescope.",
                    CharacterDescription = "The Crab — Hera sent it to distract Hercules during his fight with the Hydra. Tiny but tenacious.",
                    Stars = new[] { V(0.4f,0.55f), V(0.45f,0.6f), V(0.5f,0.55f), V(0.55f,0.6f), V(0.48f,0.5f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 1,4, 2,4 },
                    Popularity = 1,
                    StarVisuals = new[] { S(1f,.94f,.76f, .37f), S(1f,.94f,.76f, .36f), S(1f,.94f,.76f, .35f), S(1f,.94f,.76f, .36f), S(1f,.94f,.76f, .34f) },
                    StarNames = new[] { "Acubens", "Asellus Borealis", "", "Asellus Australis", "" }
                },
                new ConstellationData
                {
                    Name = "Leo",
                    ScientificDescription = "One of the easiest constellations to spot. Its brightest star Regulus sits about 79 light-years away and spins so fast it's slightly squashed.",
                    CharacterDescription = "The Lion — the Nemean (neh-MEE-an) beast from Hercules' first labor, placed among the stars by Zeus.",
                    Stars = new[] { V(0.3f,0.6f), V(0.35f,0.65f), V(0.45f,0.7f), V(0.55f,0.65f), V(0.6f,0.55f), V(0.55f,0.45f), V(0.45f,0.4f), V(0.35f,0.45f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,7, 7,0 },
                    Popularity = 3,
                    StarVisuals = new[] { S(.68f,.82f,1f, .68f), S(1f,.97f,.95f, .37f), S(1f,.72f,.42f, .55f), S(1f,.97f,.95f, .54f), S(1f,.94f,.76f, .40f), S(1f,.97f,.95f, .42f), S(1f,.97f,.95f, .38f), S(1f,.94f,.76f, .40f) },
                    StarNames = new[] { "Regulus", "", "Algieba", "Denebola", "", "", "", "" }
                },
                new ConstellationData
                {
                    Name = "Virgo",
                    ScientificDescription = "The largest zodiac constellation. Its blue-white jewel Spica (SPY-kuh) is about 250 light-years away. This region of sky is packed with distant galaxies.",
                    CharacterDescription = "The Maiden — often linked to Demeter (deh-MEE-ter), goddess of the harvest, or her daughter Persephone (per-SEF-oh-nee).",
                    Stars = new[] { V(0.5f,0.7f), V(0.45f,0.6f), V(0.5f,0.5f), V(0.55f,0.6f), V(0.4f,0.45f), V(0.6f,0.45f), V(0.5f,0.35f) },
                    LineConnections = new[] { 0,1, 0,3, 1,2, 3,2, 2,4, 2,5, 2,6 },
                    Popularity = 2,
                    StarVisuals = new[] { S(1f,.94f,.76f, .38f), S(1f,.97f,.95f, .46f), S(.68f,.82f,1f, .78f), S(1f,.94f,.76f, .44f), S(1f,.94f,.76f, .36f), S(1f,.94f,.76f, .36f), S(1f,.94f,.76f, .35f) },
                    StarNames = new[] { "", "Porrima", "Spica", "Vindemiatrix", "", "", "" }
                },
                new ConstellationData
                {
                    Name = "Libra",
                    ScientificDescription = "The only zodiac constellation that represents an object instead of a living thing. Its scale-pan stars Zubenelgenubi (zoo-BEN-el-jeh-NOO-bee) and Zubeneschamali (zoo-BEN-esh-shah-MAH-lee) used to be considered the claws of neighboring Scorpius.",
                    CharacterDescription = "The Scales — representing justice and balance, held by Astraea (as-TREE-uh), goddess of innocence.",
                    Stars = new[] { V(0.4f,0.6f), V(0.5f,0.65f), V(0.6f,0.6f), V(0.45f,0.45f), V(0.55f,0.45f) },
                    LineConnections = new[] { 0,1, 1,2, 0,3, 2,4 },
                    Popularity = 1,
                    StarVisuals = new[] { S(1f,.97f,.95f, .46f), S(.68f,.85f,1f, .48f), S(1f,.97f,.95f, .40f), S(1f,.94f,.76f, .37f), S(1f,.94f,.76f, .37f) },
                    StarNames = new[] { "Zubenelgenubi", "Zubeneschamali", "", "", "" }
                },
                new ConstellationData
                {
                    Name = "Scorpius",
                    ScientificDescription = "Its red heart Antares (an-TAIR-eez) is a supergiant hundreds of times wider than our Sun, about 550 light-years away. Named \"Rival of Mars\" for its red color.",
                    CharacterDescription = "The Scorpion — sent to defeat Orion the hunter. The two are placed on opposite sides of the sky, never visible at the same time.",
                    Stars = new[] { V(0.3f,0.65f), V(0.35f,0.6f), V(0.4f,0.55f), V(0.45f,0.5f), V(0.5f,0.45f), V(0.55f,0.4f), V(0.6f,0.35f), V(0.65f,0.38f), V(0.7f,0.42f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,7, 7,8 },
                    Popularity = 3,
                    StarVisuals = new[] { S(.68f,.82f,1f, .50f), S(.68f,.82f,1f, .44f), S(.68f,.82f,1f, .50f), S(1f,.42f,.22f, .78f), S(.68f,.82f,1f, .42f), S(.68f,.82f,1f, .42f), S(.68f,.82f,1f, .44f), S(.68f,.82f,1f, .40f), S(.75f,.85f,1f, .44f) },
                    StarNames = new[] { "Dschubba", "", "", "Antares", "", "", "Shaula", "", "" }
                },
                new ConstellationData
                {
                    Name = "Sagittarius",
                    ScientificDescription = "Point this way and you're looking toward the center of our galaxy. This patch of sky is rich with nebulae and star nurseries.",
                    CharacterDescription = "The Archer — a centaur drawing his bow at Scorpius. Often linked to the wise centaur Chiron (KYE-ron).",
                    Stars = new[] { V(0.4f,0.5f), V(0.45f,0.55f), V(0.5f,0.6f), V(0.55f,0.55f), V(0.5f,0.5f), V(0.55f,0.45f), V(0.6f,0.5f), V(0.45f,0.4f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,0, 3,5, 5,6, 4,7 },
                    Popularity = 2,
                    StarVisuals = new[] { S(.68f,.82f,1f, .58f), S(1f,.72f,.42f, .46f), S(.68f,.82f,1f, .55f), S(1f,.97f,.95f, .48f), S(1f,.97f,.95f, .44f), S(1f,.97f,.95f, .40f), S(1f,.94f,.76f, .42f), S(1f,.94f,.76f, .38f) },
                    StarNames = new[] { "Kaus Australis", "", "Nunki", "Ascella", "", "", "", "" }
                },
                new ConstellationData
                {
                    Name = "Capricornus",
                    ScientificDescription = "Say it \"KAP-rih-KOR-nus.\" A faint but ancient constellation. Its brightest star Deneb Algedi (DEN-eb al-JEE-dee) is actually two stars orbiting each other, only about 39 light-years from Earth.",
                    CharacterDescription = "The Sea Goat — half goat, half fish. The god Pan dove into the Nile and took this form to escape the monster Typhon (TYE-fon).",
                    Stars = new[] { V(0.35f,0.55f), V(0.4f,0.6f), V(0.5f,0.62f), V(0.6f,0.58f), V(0.65f,0.5f), V(0.55f,0.42f), V(0.4f,0.45f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,0 },
                    Popularity = 1,
                    StarVisuals = new[] { S(1f,.94f,.76f, .40f), S(1f,.94f,.76f, .38f), S(1f,.94f,.76f, .36f), S(1f,.97f,.95f, .44f), S(1f,.97f,.95f, .44f), S(1f,.94f,.76f, .36f), S(1f,.94f,.76f, .37f) },
                    StarNames = new[] { "Algedi", "", "", "Deneb Algedi", "Nashira", "", "" }
                },
                new ConstellationData
                {
                    Name = "Aquarius",
                    ScientificDescription = "Home to the Helix Nebula, a glowing shell of gas about 700 light-years away — one of the closest nebulae to Earth.",
                    CharacterDescription = "The Water Bearer — Ganymede (GAN-ih-meed), swept to Mount Olympus by Zeus's eagle to serve as cupbearer to the gods.",
                    Stars = new[] { V(0.4f,0.7f), V(0.45f,0.6f), V(0.5f,0.55f), V(0.55f,0.5f), V(0.5f,0.45f), V(0.45f,0.4f), V(0.55f,0.35f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 4,6 },
                    Popularity = 1,
                    StarVisuals = new[] { S(1f,.94f,.76f, .43f), S(1f,.94f,.76f, .44f), S(1f,.94f,.76f, .37f), S(1f,.94f,.76f, .36f), S(1f,.97f,.95f, .40f), S(1f,.94f,.76f, .36f), S(1f,.94f,.76f, .35f) },
                    StarNames = new[] { "Sadalmelik", "Sadalsuud", "", "", "Skat", "", "" }
                },
                new ConstellationData
                {
                    Name = "Pisces",
                    ScientificDescription = "Say it \"PIE-seez.\" A large but faint constellation. The Sun passes through here at the spring equinox, marking the start of the astronomical year.",
                    CharacterDescription = "The Fishes — Aphrodite (AF-roh-DYE-tee) and her son Eros tied themselves together as fish to escape the monster Typhon (TYE-fon).",
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
                    ScientificDescription = "The most recognizable constellation. Red supergiant Betelgeuse (BET-ul-jooz) marks one shoulder, brilliant blue Rigel (RYE-jul) the opposite foot. The three belt stars point toward the Orion Nebula, a stellar nursery.",
                    CharacterDescription = "The Hunter — placed among the stars by Zeus. His belt of three stars has guided travelers for thousands of years.",
                    Stars = new[] { V(0.35f,0.72f), V(0.6f,0.7f), V(0.4f,0.55f), V(0.45f,0.5f), V(0.5f,0.5f), V(0.55f,0.55f), V(0.38f,0.3f), V(0.58f,0.28f) },
                    LineConnections = new[] { 0,2, 2,3, 3,4, 4,5, 5,1, 2,6, 5,7, 3,4 },
                    Popularity = 3,
                    StarVisuals = new[] { S(1f,.42f,.22f, .88f), S(.68f,.82f,1f, .62f), S(.68f,.82f,1f, .52f), S(.68f,.82f,1f, .56f), S(.68f,.82f,1f, .58f), S(.68f,.82f,1f, .52f), S(.68f,.82f,1f, .54f), S(.62f,.78f,1f, .92f) },
                    StarNames = new[] { "Betelgeuse", "Bellatrix", "", "Mintaka", "Alnilam", "", "Saiph", "Rigel" }
                },
                new ConstellationData
                {
                    Name = "Ursa Major",
                    ScientificDescription = "The third-largest constellation. The Big Dipper asterism is part of it — follow pointer stars Dubhe (DOO-bee) and Merak (MAIR-ak) and they'll lead you straight to the North Star.",
                    CharacterDescription = "The Great Bear — Callisto (kah-LIS-toh), transformed into a bear by jealous Hera (HAIR-uh) and lifted into the sky by Zeus.",
                    Stars = new[] { V(0.3f,0.55f), V(0.35f,0.6f), V(0.45f,0.62f), V(0.55f,0.6f), V(0.6f,0.55f), V(0.65f,0.5f), V(0.72f,0.52f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 3,0 },
                    Popularity = 3,
                    StarVisuals = new[] { S(1f,.72f,.42f, .58f), S(1f,.97f,.95f, .50f), S(1f,.97f,.95f, .48f), S(1f,.97f,.95f, .40f), S(1f,.97f,.95f, .60f), S(1f,.97f,.95f, .55f), S(.68f,.82f,1f, .58f) },
                    StarNames = new[] { "Dubhe", "Merak", "Phecda", "Megrez", "Alioth", "Mizar", "Alkaid" }
                },
                new ConstellationData
                {
                    Name = "Ursa Minor",
                    ScientificDescription = "Polaris, our North Star, sits at the tip of the bear's tail. Ancient sailors from the Phoenicians to the Vikings navigated by it. About 433 light-years away.",
                    CharacterDescription = "The Little Bear — Arcas (AR-kus), son of Callisto, placed beside his mother in the sky forever.",
                    Stars = new[] { V(0.5f,0.75f), V(0.48f,0.65f), V(0.45f,0.55f), V(0.5f,0.48f), V(0.55f,0.5f), V(0.58f,0.55f), V(0.52f,0.58f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4, 4,5, 5,6, 6,3 },
                    Popularity = 2,
                    StarVisuals = new[] { S(1f,.94f,.76f, .55f), S(1f,.72f,.42f, .54f), S(1f,.97f,.95f, .42f), S(1f,.94f,.76f, .36f), S(1f,.94f,.76f, .35f), S(1f,.94f,.76f, .34f), S(1f,.94f,.76f, .35f) },
                    StarNames = new[] { "Polaris", "Kochab", "Pherkad", "", "", "", "" }
                },
                new ConstellationData
                {
                    Name = "Cassiopeia",
                    ScientificDescription = "Say it \"kass-ee-oh-PEE-uh.\" That famous W-shape in the northern sky — visible year-round from most northern locations. It's full of star clusters and glowing gas clouds.",
                    CharacterDescription = "The Queen — she boasted she was more beautiful than the sea nymphs, so the gods chained her throne in the sky forever.",
                    Stars = new[] { V(0.25f,0.55f), V(0.35f,0.65f), V(0.48f,0.55f), V(0.6f,0.65f), V(0.7f,0.55f) },
                    LineConnections = new[] { 0,1, 1,2, 2,3, 3,4 },
                    Popularity = 3,
                    StarVisuals = new[] { S(.68f,.82f,1f, .38f), S(1f,.97f,.95f, .46f), S(.68f,.82f,1f, .48f), S(1f,.72f,.42f, .52f), S(1f,.97f,.95f, .50f) },
                    StarNames = new[] { "Segin", "Ruchbah", "Navi", "Schedar", "Caph" }
                },
                new ConstellationData
                {
                    Name = "Cygnus",
                    ScientificDescription = "Say it \"SIG-nus.\" Also called the Northern Cross. Its brightest star Deneb (DEN-eb) is one of the most luminous stars known, blazing about 2,600 light-years away.",
                    CharacterDescription = "The Swan — Zeus in the form of a swan, flying along the Milky Way. Sometimes linked to the musician Orpheus (OR-fee-us).",
                    Stars = new[] { V(0.5f,0.75f), V(0.5f,0.6f), V(0.5f,0.45f), V(0.35f,0.55f), V(0.65f,0.55f), V(0.5f,0.3f) },
                    LineConnections = new[] { 0,1, 1,2, 2,5, 3,1, 1,4 },
                    Popularity = 2,
                    StarVisuals = new[] { S(.68f,.82f,1f, .68f), S(1f,.94f,.76f, .52f), S(1f,.72f,.42f, .48f), S(.68f,.82f,1f, .48f), S(.68f,.82f,1f, .44f), S(1f,.72f,.42f, .42f) },
                    StarNames = new[] { "Deneb", "Sadr", "", "Gienah", "", "Albireo" }
                },
                new ConstellationData
                {
                    Name = "Lyra",
                    ScientificDescription = "Say it \"LYE-ruh.\" Small but brilliant. Vega (VEE-guh) is the fifth brightest star in the night sky and was our North Star about 12,000 years ago. The Ring Nebula hides here too.",
                    CharacterDescription = "The Lyre — the magical harp of Orpheus (OR-fee-us), whose music could charm stones and rivers.",
                    Stars = new[] { V(0.5f,0.72f), V(0.42f,0.58f), V(0.58f,0.58f), V(0.4f,0.45f), V(0.6f,0.45f) },
                    LineConnections = new[] { 0,1, 0,2, 1,3, 2,4, 3,4 },
                    Popularity = 2,
                    StarVisuals = new[] { S(.68f,.82f,1f, .90f), S(.68f,.82f,1f, .37f), S(.68f,.82f,1f, .40f), S(.68f,.82f,1f, .36f), S(.68f,.82f,1f, .34f) },
                    StarNames = new[] { "Vega", "Sheliak", "Sulafat", "", "" }
                },
                new ConstellationData
                {
                    Name = "Canis Major",
                    ScientificDescription = "Home to Sirius (SEER-ee-us), the brightest star in the entire night sky — only 8.6 light-years away. Its rising once marked the start of the hottest \"dog days\" of summer.",
                    CharacterDescription = "The Greater Dog — Orion's faithful hunting companion, trotting loyally after him across the sky.",
                    Stars = new[] { V(0.5f,0.7f), V(0.45f,0.6f), V(0.4f,0.5f), V(0.55f,0.55f), V(0.6f,0.45f), V(0.5f,0.35f) },
                    LineConnections = new[] { 0,1, 1,2, 0,3, 3,4, 4,5, 1,3 },
                    Popularity = 2,
                    StarVisuals = new[] { S(.85f,.9f,1f, 1f), S(.68f,.82f,1f, .55f), S(1f,.94f,.76f, .38f), S(1f,.94f,.76f, .58f), S(.68f,.82f,1f, .62f), S(.68f,.82f,1f, .48f) },
                    StarNames = new[] { "Sirius", "Mirzam", "", "Wezen", "Adhara", "Aludra" }
                },
                new ConstellationData
                {
                    Name = "Draco",
                    ScientificDescription = "A long chain of stars looping around the Little Bear. Its star Thuban (THOO-bahn) was the North Star when the ancient Egyptians built the pyramids.",
                    CharacterDescription = "The Dragon — Ladon (LAY-don), the many-headed guardian of the golden apples in the garden of the Hesperides (hes-PAIR-ih-deez).",
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
