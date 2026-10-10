using System;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The crops that grow in the plant pots - wheat, corn, tomato and cabbage - modelled in code in
    /// the game's smooth low-poly style, four stages each: a sprout, a young plant, the grown plant
    /// (wheat heading, corn tasselling, tomato in flower, cabbage hearting) and the ripe crop.
    /// <see cref="CropSprites"/> renders them into the sprites the pots show.
    ///
    /// Each model stands on its base at the origin - the crown of the pot's soil - with +Y up. The
    /// game camera looks from the +X, -Z side, so a yaw of 143 degrees points a leaf straight at it,
    /// 53 across to the right of the screen and 233 to the left; leaves are spread to read from there.
    /// Shapes are chunkier than life, so each crop still reads at the game's zoom.
    ///
    /// A pot grows a small field of one crop, not a single plant: <see cref="Spots"/> sets the plants
    /// out on the soil's rows, and each stage is built in a few <see cref="Variants"/> (the same plant
    /// grown from another seed) so neighbours differ. Every stage, the sprout included, fills the pot.
    ///
    /// What each crop gives at harvest - the produce its item icon shows - is modelled alongside, in
    /// CropModels.Produce.cs, and the dishes cooked from them in CropModels.Dishes.cs.
    /// </summary>
    public static partial class CropModels
    {
        public const int StageCount = 4;

        /// <summary>How many versions of each stage are rendered; each plant in a pot shows one.</summary>
        public const int Variants = 3;

        public enum Crop { Wheat, Corn, Tomato, Cabbage }

        /// <summary>One material each: a colour running from base to tip, and what else it paints.</summary>
        public enum Paint
        {
            WheatBlade, WheatGreen, WheatGold, WheatStraw, GrainGreen, GrainGold,
            CornLeaf, CornStalk, Husk, Cob, Tassel, Silk,
            TomatoStem, TomatoLeaf, TomatoGreen, TomatoOrange, TomatoRed, Flower, Stake, Twine,
            Seedling, CabbageYoung, CabbageOuter, CabbageInner, CabbageHead, CabbageCore,
            // The produce in the item icons.
            CobBig, Burlap, BurlapDark, Flour,
            // The dishes in the recipe icons.
            BreadCrust, BreadCrumb, BreadScore, BoardWood, CornbreadCrumb, CornbreadCrust, Butter, Kernel, Glaze, GlazeBand, Soup, SoupCream, Basil,
            Crouton,
            // The salads, and the ingredients the salad station keeps.
            Lettuce, LettuceHeart, TomatoFlesh, TomatoSeeds, Carrot, CarrotTop, Cheese, CheeseRind, Bean, BeanWhite, Chicken, ChickenCut,
            GrillMark, Bone, Can, CanLabel, CanBand,
            // The farmyard's eggs and milk, and the dishes made with them.
            EggShell, EggShellCream, Milk, MilkGlass, Custard, CustardTop, Pastry, Chowder,
            // The griddle's and the grill's dishes, and the potatoes, onions and fish they need.
            EggWhite, EggWhiteCrisp, Yolk, HashBrown, HashBrownCrisp, FrenchToast, Syrup, PotatoSkin, PotatoFlesh, OnionSkin, Onion,
            OnionRing, OnionGrilled, FishSkin, FishGrilled, FishFin, Skewer, CobGrilled, Lemon, LemonFlesh,
            Count,
        }

        public struct Swatch
        {
            public Color Base, Tip;
            public float Power, Vein, Gloss;

            /// <summary>How much darker a leaf's edges are: the line where one leaf lies over another.</summary>
            public float Edge;
            public Vector3 Kernels;

            /// <summary>How much further round the form the light reaches than on a leaf: soft things - a
            /// loaf's crumb - that scatter it, and stay warm turned away from the sun.</summary>
            public float Wrap;
        }

        public static Swatch Of(Paint paint)
        {
            switch (paint)
            {
                case Paint.WheatBlade: return S(0.27f, 0.5f, 0.17f, 0.56f, 0.76f, 0.27f, vein: 0.25f);
                case Paint.WheatGreen: return S(0.34f, 0.56f, 0.19f, 0.6f, 0.79f, 0.31f);
                case Paint.WheatGold: return S(0.66f, 0.5f, 0.19f, 0.95f, 0.8f, 0.38f);
                case Paint.WheatStraw: return S(0.55f, 0.46f, 0.22f, 0.84f, 0.71f, 0.38f, vein: 0.2f);
                case Paint.GrainGreen: return S(0.46f, 0.66f, 0.25f, 0.7f, 0.86f, 0.4f, gloss: 0.15f);
                case Paint.GrainGold: return S(0.84f, 0.6f, 0.2f, 1f, 0.87f, 0.46f, gloss: 0.3f);

                case Paint.CornLeaf: return S(0.19f, 0.43f, 0.16f, 0.54f, 0.77f, 0.27f, vein: 0.45f);
                case Paint.CornStalk: return S(0.29f, 0.5f, 0.18f, 0.52f, 0.72f, 0.26f);
                case Paint.Husk: return S(0.4f, 0.62f, 0.24f, 0.66f, 0.82f, 0.4f, vein: 0.3f, edge: 0.12f);
                case Paint.Cob:
                    Swatch cob = S(0.93f, 0.66f, 0.14f, 1f, 0.85f, 0.32f, gloss: 0.3f);
                    cob.Kernels = new Vector3(12f, 11f, 0.3f);
                    return cob;
                case Paint.Tassel: return S(0.64f, 0.48f, 0.2f, 0.92f, 0.78f, 0.42f);
                case Paint.Silk: return S(0.66f, 0.35f, 0.16f, 0.9f, 0.6f, 0.3f);

                case Paint.TomatoStem: return S(0.25f, 0.45f, 0.16f, 0.43f, 0.65f, 0.23f);
                case Paint.TomatoLeaf: return S(0.18f, 0.39f, 0.15f, 0.42f, 0.66f, 0.22f, vein: 0.35f);
                case Paint.TomatoGreen: return S(0.45f, 0.65f, 0.2f, 0.66f, 0.82f, 0.32f, gloss: 0.7f);
                case Paint.TomatoOrange: return S(0.87f, 0.41f, 0.1f, 0.98f, 0.62f, 0.2f, gloss: 0.8f);
                case Paint.TomatoRed: return S(0.78f, 0.1f, 0.07f, 0.98f, 0.3f, 0.16f, gloss: 0.9f);
                case Paint.Flower: return S(0.95f, 0.72f, 0.12f, 1f, 0.9f, 0.4f);
                case Paint.Stake: return S(0.45f, 0.3f, 0.17f, 0.64f, 0.46f, 0.27f);
                case Paint.Twine: return S(0.76f, 0.64f, 0.4f, 0.9f, 0.8f, 0.55f);

                case Paint.Seedling: return S(0.35f, 0.61f, 0.2f, 0.64f, 0.86f, 0.34f, vein: 0.3f);
                case Paint.CabbageYoung: return S(0.24f, 0.5f, 0.28f, 0.52f, 0.76f, 0.4f, vein: 0.5f, edge: 0.12f);
                case Paint.CabbageOuter: return S(0.19f, 0.43f, 0.3f, 0.47f, 0.71f, 0.42f, vein: 0.6f, power: 0.8f, edge: 0.15f);
                case Paint.CabbageInner: return S(0.36f, 0.62f, 0.33f, 0.64f, 0.84f, 0.48f, vein: 0.45f, edge: 0.15f);
                case Paint.CabbageHead: return S(0.56f, 0.77f, 0.42f, 0.8f, 0.92f, 0.58f, vein: 0.35f, edge: 0.3f);
                case Paint.CabbageCore: return S(0.66f, 0.84f, 0.5f, 0.84f, 0.94f, 0.62f);

                // A cob seen close up: kernels modelled one by one, so the grooves between them are lighter.
                case Paint.CobBig:
                    Swatch big = S(0.95f, 0.62f, 0.1f, 1f, 0.84f, 0.3f, gloss: 0.45f);
                    big.Kernels = new Vector3(ProduceKernelColumns, ProduceKernelRows, 0.22f);
                    return big;

                // Sacking: a loose weave, warm and light toward the top where the sun catches it.
                case Paint.Burlap:
                    Swatch burlap = S(0.66f, 0.47f, 0.27f, 0.88f, 0.72f, 0.48f, power: 0.8f);
                    burlap.Kernels = new Vector3(72f, 44f, 0.06f);
                    return burlap;
                case Paint.BurlapDark: return S(0.6f, 0.42f, 0.24f, 0.8f, 0.63f, 0.41f);
                case Paint.Flour: return S(0.88f, 0.83f, 0.74f, 0.97f, 0.94f, 0.86f);

                // A loaf's crust, pale gold where it sat on the tray to deep golden brown on top; its crumb.
                case Paint.BreadCrust: return S(0.84f, 0.58f, 0.3f, 0.6f, 0.33f, 0.14f, gloss: 0.25f, power: 0.9f);
                case Paint.BreadCrumb:
                    Swatch crumb = S(0.99f, 0.92f, 0.74f, 0.95f, 0.83f, 0.58f);
                    crumb.Wrap = 0.55f;
                    return crumb;
                // The paler dough the scoring opens up along the top of the loaf.
                case Paint.BreadScore: return S(0.93f, 0.76f, 0.46f, 0.97f, 0.84f, 0.56f);
                case Paint.BoardWood: return S(0.46f, 0.29f, 0.16f, 0.56f, 0.36f, 0.2f);

                // Cornbread: bright yellow where it is cut, a golden-brown crust baked on top.
                case Paint.CornbreadCrumb: return S(0.97f, 0.78f, 0.28f, 1f, 0.84f, 0.36f);
                case Paint.CornbreadCrust: return S(0.86f, 0.56f, 0.2f, 0.76f, 0.44f, 0.13f, gloss: 0.15f);
                case Paint.Butter: return S(1f, 0.88f, 0.48f, 1f, 0.96f, 0.72f, gloss: 0.6f);
                case Paint.Kernel: return S(0.97f, 0.72f, 0.14f, 1f, 0.86f, 0.3f, gloss: 0.5f);

                // The diner's crockery: cream glaze with a teal band, the colours of its floor.
                case Paint.Glaze: return S(0.93f, 0.89f, 0.8f, 0.98f, 0.95f, 0.88f, gloss: 0.5f);
                case Paint.GlazeBand: return S(0.33f, 0.55f, 0.5f, 0.38f, 0.6f, 0.55f, gloss: 0.5f);

                // Tomato soup, darker toward the bowl's side, a swirl of cream, basil and croutons.
                case Paint.Soup: return S(0.72f, 0.16f, 0.07f, 0.9f, 0.32f, 0.12f, gloss: 0.7f);
                case Paint.SoupCream: return S(0.95f, 0.9f, 0.82f, 1f, 0.97f, 0.92f, gloss: 0.3f);
                case Paint.Basil: return S(0.2f, 0.45f, 0.14f, 0.4f, 0.66f, 0.22f, vein: 0.4f, gloss: 0.3f);
                case Paint.Crouton: return S(0.85f, 0.6f, 0.28f, 0.93f, 0.72f, 0.4f);

                // Lettuce: pale at the rib, fresh green toward the frilled edge; the paler heart.
                case Paint.Lettuce: return S(0.62f, 0.8f, 0.38f, 0.33f, 0.6f, 0.19f, vein: 0.4f, edge: 0.14f);
                case Paint.LettuceHeart: return S(0.74f, 0.86f, 0.47f, 0.52f, 0.74f, 0.28f, vein: 0.3f, edge: 0.12f);

                // A tomato cut open: the wet red flesh and the paler jelly round the seeds.
                case Paint.TomatoFlesh: return S(0.8f, 0.15f, 0.09f, 0.88f, 0.24f, 0.14f, gloss: 0.5f);
                case Paint.TomatoSeeds: return S(0.93f, 0.48f, 0.27f, 0.97f, 0.64f, 0.38f, gloss: 0.8f);

                case Paint.Carrot: return S(0.86f, 0.4f, 0.1f, 0.95f, 0.54f, 0.16f, gloss: 0.3f);
                case Paint.CarrotTop: return S(0.24f, 0.48f, 0.16f, 0.4f, 0.66f, 0.22f, vein: 0.25f);

                // A firm yellow cheese, its waxed rind a deeper gold.
                case Paint.Cheese:
                    Swatch cheese = S(0.95f, 0.8f, 0.44f, 1f, 0.88f, 0.56f, gloss: 0.2f);
                    cheese.Wrap = 0.25f;
                    return cheese;
                case Paint.CheeseRind: return S(0.82f, 0.56f, 0.2f, 0.88f, 0.64f, 0.26f, gloss: 0.4f);

                // Kidney beans and white beans, glossy from the tin.
                case Paint.Bean: return S(0.44f, 0.11f, 0.09f, 0.6f, 0.2f, 0.15f, gloss: 0.7f);
                case Paint.BeanWhite: return S(0.88f, 0.83f, 0.71f, 0.96f, 0.93f, 0.84f, gloss: 0.5f);

                // Grilled chicken: golden brown outside, pale where it is sliced, the bars of the grill.
                case Paint.Chicken: return S(0.76f, 0.48f, 0.22f, 0.9f, 0.66f, 0.36f, gloss: 0.3f);
                case Paint.ChickenCut:
                    Swatch cut = S(0.95f, 0.86f, 0.7f, 0.99f, 0.93f, 0.82f);
                    cut.Wrap = 0.4f;
                    return cut;
                case Paint.GrillMark: return S(0.34f, 0.19f, 0.1f, 0.42f, 0.25f, 0.13f);
                case Paint.Bone: return S(0.88f, 0.84f, 0.74f, 0.97f, 0.94f, 0.87f, gloss: 0.3f);

                // A tin of beans: steel rims, a red label with a cream band.
                case Paint.Can: return S(0.58f, 0.61f, 0.64f, 0.8f, 0.83f, 0.86f, gloss: 0.8f);
                case Paint.CanLabel: return S(0.66f, 0.18f, 0.15f, 0.76f, 0.25f, 0.2f, gloss: 0.25f);
                case Paint.CanBand: return S(0.92f, 0.87f, 0.75f, 0.97f, 0.93f, 0.84f, gloss: 0.25f);

                // Eggs: a warm brown shell and a cream one, a little lighter toward the top.
                case Paint.EggShell: return S(0.66f, 0.45f, 0.29f, 0.84f, 0.64f, 0.45f, gloss: 0.35f);
                case Paint.EggShellCream: return S(0.86f, 0.8f, 0.68f, 0.97f, 0.93f, 0.84f, gloss: 0.35f);

                // Milk, soft and very white; the glass round its neck catching the light.
                case Paint.Milk:
                    Swatch milk = S(0.86f, 0.86f, 0.83f, 0.99f, 0.98f, 0.95f, gloss: 0.45f);
                    milk.Wrap = 0.3f;
                    return milk;
                case Paint.MilkGlass: return S(0.62f, 0.72f, 0.74f, 0.86f, 0.92f, 0.93f, gloss: 0.9f);

                // A custard tart: the pale custard, its top caught brown in the oven, the short pastry.
                case Paint.Custard:
                    Swatch custard = S(0.95f, 0.84f, 0.52f, 0.99f, 0.92f, 0.66f, gloss: 0.4f);
                    custard.Wrap = 0.35f;
                    return custard;
                case Paint.CustardTop: return S(0.7f, 0.46f, 0.2f, 0.82f, 0.58f, 0.28f, gloss: 0.3f);
                case Paint.Pastry: return S(0.86f, 0.64f, 0.34f, 0.95f, 0.78f, 0.48f, gloss: 0.15f);

                // Corn chowder: creamy and pale gold.
                case Paint.Chowder:
                    Swatch chowder = S(0.9f, 0.8f, 0.52f, 0.98f, 0.91f, 0.68f, gloss: 0.55f);
                    chowder.Wrap = 0.3f;
                    return chowder;

                // A fried egg: the set white, its lacy edge crisped brown on the griddle, the glossy yolk.
                case Paint.EggWhite:
                    Swatch white = S(0.88f, 0.87f, 0.82f, 0.98f, 0.97f, 0.93f, gloss: 0.5f);
                    white.Wrap = 0.3f;
                    return white;
                case Paint.EggWhiteCrisp: return S(0.72f, 0.52f, 0.28f, 0.88f, 0.72f, 0.48f);
                case Paint.Yolk: return S(0.93f, 0.56f, 0.06f, 1f, 0.76f, 0.2f, gloss: 0.85f);

                // Hash browns: golden shreds, the crisp ones a deeper brown.
                case Paint.HashBrown: return S(0.8f, 0.56f, 0.24f, 0.93f, 0.72f, 0.38f, gloss: 0.2f);
                case Paint.HashBrownCrisp: return S(0.6f, 0.36f, 0.14f, 0.78f, 0.5f, 0.2f, gloss: 0.25f);

                // French toast: bread soaked in egg and fried golden, maple syrup over it.
                case Paint.FrenchToast: return S(0.82f, 0.56f, 0.26f, 0.94f, 0.73f, 0.4f, gloss: 0.3f);
                case Paint.Syrup: return S(0.48f, 0.22f, 0.06f, 0.72f, 0.4f, 0.12f, gloss: 0.95f);

                case Paint.PotatoSkin: return S(0.58f, 0.43f, 0.27f, 0.76f, 0.6f, 0.4f, gloss: 0.15f);
                case Paint.PotatoFlesh:
                    Swatch potato = S(0.9f, 0.82f, 0.56f, 0.98f, 0.92f, 0.7f, gloss: 0.3f);
                    potato.Wrap = 0.25f;
                    return potato;

                // An onion: papery golden-brown skin; white rings inside, a shade greener between them; browned on the grill.
                case Paint.OnionSkin: return S(0.58f, 0.33f, 0.14f, 0.82f, 0.56f, 0.28f, gloss: 0.45f, vein: 0.3f);
                case Paint.Onion: return S(0.88f, 0.85f, 0.74f, 0.97f, 0.95f, 0.86f, gloss: 0.45f);
                case Paint.OnionRing: return S(0.78f, 0.78f, 0.6f, 0.86f, 0.86f, 0.68f, gloss: 0.4f);
                case Paint.OnionGrilled: return S(0.74f, 0.56f, 0.34f, 0.9f, 0.76f, 0.52f, gloss: 0.4f);

                // A fish, pale along its belly to blue-grey along its back; grilled golden; its fins.
                case Paint.FishSkin: return S(0.84f, 0.85f, 0.82f, 0.36f, 0.43f, 0.5f, gloss: 0.75f);
                case Paint.FishGrilled: return S(0.86f, 0.72f, 0.5f, 0.64f, 0.44f, 0.24f, gloss: 0.5f);
                case Paint.FishFin: return S(0.42f, 0.45f, 0.46f, 0.6f, 0.62f, 0.6f, gloss: 0.3f);

                case Paint.Skewer: return S(0.64f, 0.5f, 0.32f, 0.82f, 0.68f, 0.48f);

                // A husked cob off the grill: kernels a deeper gold, charred in patches.
                case Paint.CobGrilled:
                    Swatch grilled = S(0.86f, 0.56f, 0.14f, 0.97f, 0.76f, 0.28f, gloss: 0.45f);
                    grilled.Kernels = new Vector3(ProduceKernelColumns, ProduceKernelRows, 0.22f);
                    return grilled;

                case Paint.Lemon: return S(0.88f, 0.74f, 0.16f, 0.98f, 0.88f, 0.34f, gloss: 0.5f);
                case Paint.LemonFlesh: return S(0.95f, 0.88f, 0.48f, 1f, 0.96f, 0.68f, gloss: 0.6f);
                default: return S(1f, 0f, 1f, 1f, 0f, 1f);
            }
        }

        static Swatch S(float r0, float g0, float b0, float r1, float g1, float b1, float vein = 0f, float gloss = 0f, float power = 1f,
                        float edge = 0f) =>
            new Swatch { Base = new Color(r0, g0, b0), Tip = new Color(r1, g1, b1), Vein = vein, Gloss = gloss, Power = power, Edge = edge };

        /// <summary>
        /// How much bigger than modelled a crop is shown. Several plants share a pot, so each is sized
        /// to its spot: the ripe ones meet their neighbours and the pot reads as one full patch.
        /// </summary>
        public static float DisplayScale(Crop crop)
        {
            switch (crop)
            {
                case Crop.Tomato: return 1.12f;
                default: return 1f;
            }
        }

        /// <summary>The soil's rows, in metres north of the cell's middle: three, a third of a metre apart.</summary>
        const float Row = 1f / 3f;

        /// <summary>
        /// Where the plants of <paramref name="crop"/> stand in a pot, in metres from the middle of its
        /// cell (x east, y north), on the soil's three rows: wheat sown thick, three clumps a row; the
        /// bigger crops staggered five to a pot, two on each outer row and one between.
        /// </summary>
        public static Vector2[] Spots(Crop crop)
        {
            if (crop == Crop.Wheat)
            {
                return new[]
                {
                    new Vector2(-0.3f, Row), new Vector2(0f, Row), new Vector2(0.28f, Row),
                    new Vector2(-0.27f, 0f), new Vector2(0.03f, 0f), new Vector2(0.31f, 0f),
                    new Vector2(-0.3f, -Row), new Vector2(0f, -Row), new Vector2(0.28f, -Row),
                };
            }

            return new[]
            {
                new Vector2(-0.24f, Row), new Vector2(0.24f, Row),
                new Vector2(0f, 0f),
                new Vector2(-0.24f, -Row), new Vector2(0.24f, -Row),
            };
        }

        /// <summary>How much plants of <paramref name="crop"/> differ in size, either way.</summary>
        public static float SizeJitter(Crop crop) => crop == Crop.Wheat ? 0.14f : 0.09f;

        /// <summary>The model of <paramref name="crop"/> at <paramref name="stage"/> (0 the sprout, 3 ripe), grown from seed <paramref name="variant"/>.</summary>
        public static SmoothMesh Build(Crop crop, int stage, int variant = 0)
        {
            var mesh = new SmoothMesh((int)Paint.Count);
            var dice = new Dice(7919 * ((int)crop + 1) + stage * 31 + variant * 104729);

            switch (crop)
            {
                case Crop.Wheat: Wheat(mesh, stage, dice); break;
                case Crop.Corn: Corn(mesh, stage, dice); break;
                case Crop.Tomato: Tomato(mesh, stage, dice); break;
                case Crop.Cabbage: Cabbage(mesh, stage, dice); break;
            }

            return mesh;
        }

        // --- Wheat ------------------------------------------------------------------------------------

        static void Wheat(SmoothMesh m, int stage, Dice d)
        {
            switch (stage)
            {
                case 0:
                    // A tuft of fresh shoots, fanning out.
                    Tuft(m, d, Paint.Seedling, 10, 0.025f, 0.085f, 0.135f, 0.0095f, 35f, 66f);
                    break;

                case 1:
                    Tuft(m, d, Paint.WheatBlade, 16, 0.035f, 0.14f, 0.21f, 0.0125f, 8f, 46f);
                    break;

                case 2:
                    Tuft(m, d, Paint.WheatBlade, 11, 0.04f, 0.15f, 0.21f, 0.0125f, 0f, 30f);
                    Stalks(m, d, Paint.WheatGreen, Paint.GrainGreen, 9, 0.29f, 0.36f, 0.08f, 1.05f, 0f, 10f, false);
                    break;

                default:
                    // Golden heads nodding over a little green still at the foot.
                    Tuft(m, d, Paint.WheatBlade, 4, 0.03f, 0.1f, 0.14f, 0.012f, 0f, 25f);
                    Tuft(m, d, Paint.WheatStraw, 6, 0.045f, 0.13f, 0.18f, 0.012f, -10f, 20f);
                    Stalks(m, d, Paint.WheatGold, Paint.GrainGold, 12, 0.36f, 0.45f, 0.095f, 1.2f, 18f, 40f, true);
                    break;
            }
        }

        static void Tuft(SmoothMesh m, Dice d, Paint paint, int count, float spread, float minLength, float maxLength, float width,
                         float minTip, float maxTip)
        {
            for (int i = 0; i < count; i++)
            {
                float yaw = i * 137.5f + d.Range(-15f, 15f);
                Vector3 at = Ground(yaw, d.Range(0f, spread));
                Blade(m, paint, at, yaw + d.Range(-25f, 25f), d.Range(minLength, maxLength), width, d.Range(70f, 86f), d.Range(minTip, maxTip));
            }
        }

        /// <summary>A clump of stalks, leaning out from the middle, each with an ear that nods over as it ripens.</summary>
        static void Stalks(SmoothMesh m, Dice d, Paint stalk, Paint grain, int count, float minHeight, float maxHeight, float earLength,
                           float grainScale, float minNod, float maxNod, bool awns)
        {
            for (int i = 0; i < count; i++)
            {
                float yaw = i * 137.5f + d.Range(-20f, 20f);
                float r = Mathf.Sqrt((i + 0.5f) / count) * 0.055f;
                Vector3 at = Ground(yaw, r) + Vector3.down * 0.01f;
                float lean = Mathf.Lerp(3f, 14f, r / 0.055f) + d.Range(-2f, 2f);
                float nod = d.Range(minNod, maxNod);
                float height = d.Range(minHeight, maxHeight);

                Spine stem = Spine.Arc(at, yaw, height, 90f - lean, 90f - lean - nod * 0.35f);
                Stem(m, stalk, stem, 0.0068f, 0.0055f, 6, 10);

                // A leaf or two up the stalk.
                if (i % 3 == 0)
                    Blade(m, stalk == Paint.WheatGold ? Paint.WheatStraw : Paint.WheatBlade, stem.At(0.35f), yaw + d.Range(-60f, 60f),
                          height * 0.4f, 0.01f, 60f, 5f);

                Spine ear = Spine.Arc(stem.End, yaw, earLength, 90f - lean - nod * 0.35f, 90f - lean - nod);
                Stem(m, stalk, ear, 0.0055f, 0.0025f, 6, 6);
                Ear(m, grain, stalk, ear, grainScale, awns);
            }
        }

        /// <summary>Grains up an ear in two rows either side, plumpest in the middle.</summary>
        static void Ear(SmoothMesh m, Paint grain, Paint stalk, Spine ear, float scale, bool awns)
        {
            const int rows = 7;
            for (int k = 0; k < rows; k++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float t = (k + (side > 0 ? 0.3f : 0.8f)) / rows;
                    Vector3 p = ear.At(t), tangent = ear.Tangent(t), across = ear.Side(t);
                    Vector3 normal = Vector3.Cross(tangent, across);

                    float size = scale * Mathf.Lerp(0.55f, 1f, Mathf.Sin(Mathf.PI * Mathf.Lerp(0.12f, 0.95f, t)));
                    Vector3 outward = (across * (side * 0.85f) + normal * ((k % 2 == 0 ? 0.5f : -0.5f))).normalized;
                    Vector3 axis = (tangent + outward * 0.5f).normalized;
                    Vector3 centre = p + outward * 0.006f * size + axis * 0.004f;

                    Blob(m, grain, centre, new Vector3(0.0075f, 0.0135f, 0.0082f) * size, Quaternion.LookRotation(outward, axis), 8, 6);

                    if (awns && k >= 2)
                    {
                        Vector3 from = centre + axis * 0.011f * size;
                        Stem(m, grain, Spine.Line(from, from + (axis + outward * 0.25f).normalized * 0.035f), 0.0016f, 0.0004f, 4, 3);
                    }
                }
            }

            // A grain to finish the tip.
            Vector3 tip = ear.End;
            Blob(m, grain, tip, new Vector3(0.006f, 0.011f, 0.006f) * scale, Quaternion.LookRotation(ear.Side(1f), ear.Tangent(1f)), 8, 6);
            if (awns)
                Stem(m, grain, Spine.Line(tip, tip + ear.Tangent(1f) * 0.04f), 0.0016f, 0.0004f, 4, 3);
        }

        // --- Corn ------------------------------------------------------------------------------------

        static void Corn(SmoothMesh m, int stage, Dice d)
        {
            float yaw = 50f + d.Range(-8f, 8f);

            switch (stage)
            {
                case 0:
                    Sown(d, 3, 0.05f, (at, turn, size) =>
                    {
                        Stem(m, Paint.CornStalk, Spine.Line(at + Vector3.down * 0.01f, at + Vector3.up * 0.03f * size), 0.011f * size, 0.01f * size, 8, 2);
                        Blade(m, Paint.Seedling, at + Vector3.up * 0.016f * size, yaw + turn, 0.12f * size, 0.024f * size, 70f, 28f, 0.3f);
                        Blade(m, Paint.Seedling, at + Vector3.up * 0.022f * size, yaw + turn + 180f + d.Range(-15f, 15f), 0.11f * size,
                              0.023f * size, 74f, 34f, 0.3f);
                        Blade(m, Paint.CornLeaf, at + Vector3.up * 0.026f * size, yaw + turn + 90f + d.Range(-20f, 20f), 0.08f * size,
                              0.016f * size, 86f, 70f, 0.6f);
                    });
                    break;

                case 1:
                    CornPlant(m, d, yaw, 0.12f, 0.016f, 6, 0.19f, 0.27f, 0.034f, 55f, 10f);
                    break;

                // The ears lean out across the screen: pointed at the camera they would show only their ends.
                case 2:
                {
                    Spine stalk = CornPlant(m, d, yaw, 0.4f, 0.02f, 7, 0.23f, 0.31f, 0.04f, 58f, -10f);
                    Tassel(m, d, stalk.End, 4, 0.04f, 0.06f, Paint.CornStalk);
                    CobOnStalk(m, d, stalk, 0.44f, yaw + 12f, 0.11f, 0.027f, true);
                    break;
                }

                default:
                {
                    Spine stalk = CornPlant(m, d, yaw, 0.6f, 0.024f, 9, 0.27f, 0.35f, 0.044f, 58f, -18f);
                    Tassel(m, d, stalk.End, 6, 0.08f, 0.11f, Paint.Tassel);
                    CobOnStalk(m, d, stalk, 0.4f, yaw + 15f, 0.17f, 0.037f, false);
                    CobOnStalk(m, d, stalk, 0.56f, yaw + 168f, 0.15f, 0.034f, false);
                    break;
                }
            }
        }

        /// <summary>A stalk with its leaves on two sides, alternating up it - the long low ones arching over, the top ones upright.</summary>
        static Spine CornPlant(SmoothMesh m, Dice d, float yaw, float height, float radius, int leaves, float topLength, float lowLength,
                               float width, float rise, float droop)
        {
            Spine stalk = Spine.Arc(Vector3.down * 0.01f, yaw + 90f, height, 89f, 87f);
            Stem(m, Paint.CornStalk, stalk, radius, radius * 0.6f, 10, 12);

            // Brace roots flaring into the soil at the foot of a grown stalk.
            if (height > 0.4f)
                for (int k = 0; k < 5; k++)
                {
                    float a = k * 72f + 20f;
                    Vector3 from = stalk.At(0.06f) + Horizontal(a) * radius * 0.6f;
                    Stem(m, Paint.CornStalk, Spine.Arc(from, a, 0.06f, -40f, -70f), radius * 0.28f, radius * 0.18f, 6, 5);
                }

            for (int i = 0; i < leaves; i++)
            {
                float t = Mathf.Lerp(0.08f, 0.92f, i / (float)Mathf.Max(1, leaves - 1));
                float up = i / (float)Mathf.Max(1, leaves - 1);
                float side = (i % 2 == 0 ? 0f : 180f) + d.Range(-22f, 22f);
                Vector3 at = stalk.At(t) + Horizontal(yaw + side) * radius * 0.5f;
                float length = Mathf.Lerp(lowLength, topLength, up) * d.Range(0.9f, 1.08f);

                Blade(m, Paint.CornLeaf, at, yaw + side, length, width * Mathf.Lerp(1f, 0.8f, up),
                      Mathf.Lerp(rise, 80f, up * up), Mathf.Lerp(droop, 35f, up * up), 0.28f, 0.03f);
            }

            // The rolled leaves still unfurling at the top.
            for (int k = 0; k < 2; k++)
                Blade(m, Paint.CornLeaf, stalk.End + Vector3.down * 0.02f, yaw + k * 180f + 40f, topLength * 0.35f, width * 0.45f, 86f, 70f, 0.8f);

            return stalk;
        }

        static void Tassel(SmoothMesh m, Dice d, Vector3 top, int branches, float minLength, float maxLength, Paint paint)
        {
            Stem(m, paint, Spine.Arc(top + Vector3.down * 0.01f, 0f, maxLength * 1.1f, 88f, 80f), 0.006f, 0.002f, 6, 6);
            for (int i = 0; i < branches; i++)
            {
                float a = i * 137.5f + d.Range(-10f, 10f);
                Vector3 from = top + Vector3.up * d.Range(0.005f, 0.03f);
                Stem(m, paint, Spine.Arc(from, a, d.Range(minLength, maxLength), d.Range(40f, 60f), d.Range(-25f, 0f)), 0.0045f, 0.0015f, 5, 8);
            }
        }

        /// <summary>
        /// An ear of corn leaning out from the stalk. A young one is still shut in its husk, drawn to a
        /// point with the silk at its tip; a ripe one has the husk peeled back and hanging open round
        /// its foot, the kernels bare above it - the ear as every harvest picture shows it.
        /// </summary>
        static void CobOnStalk(SmoothMesh m, Dice d, Spine stalk, float t, float yaw, float length, float radius, bool young)
        {
            Vector3 axis = Direction(yaw, 55f);
            Vector3 foot = stalk.At(t) + Horizontal(yaw) * radius * 0.7f;
            Vector3 across = Across(yaw);
            Vector3 over = Vector3.Cross(axis, across).normalized;
            Vector3 Around(float angle) => across * Mathf.Cos(angle) + over * Mathf.Sin(angle);

            float cobLength = length * (young ? 0.85f : 1f);
            m.Surface((int)Paint.Cob, 14, 12, (u, v) =>
            {
                float r = radius * 0.9f * Mathf.Pow(Arch(Mathf.PI * Mathf.Lerp(0.04f, 1f, v)), 0.3f) * (1f - 0.25f * v);
                return foot + axis * (v * cobLength) + Around(u * Mathf.PI * 2f) * r;
            }, wrapU: true);

            const int husks = 4;
            for (int k = 0; k < husks; k++)
            {
                float centre = (k + 0.5f) * Mathf.PI * 2f / husks + d.Range(-0.25f, 0.25f);

                if (young)
                {
                    // Shut: round the whole cob and past its tip, closing to a point.
                    m.Surface((int)Paint.Husk, 6, 12, (u, v) =>
                    {
                        float x = u * 2f - 1f;
                        float r = radius * (1.02f + 0.03f * k) * Mathf.Lerp(1f, 0.25f, v * v * v);
                        float a = centre + x * 1.05f;
                        return foot + axis * (v * length * 1.08f) + Around(a) * r;
                    });
                    continue;
                }

                // Peeled: up the cob a little way, then out and back down, hanging open.
                float reach = d.Range(0.9f, 1.1f);
                m.Surface((int)Paint.Husk, 6, 14, (u, v) =>
                {
                    float x = u * 2f - 1f;
                    float along = length * 0.34f * reach * Mathf.Sin(v * Mathf.PI * 0.78f);
                    float r = radius * (1.05f + 2.3f * reach * Mathf.Pow(Mathf.SmoothStep(0f, 1f, (v - 0.2f) / 0.8f), 1.4f));
                    float width = radius * 1.25f * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(v, 2.5f)), 0.6f);
                    float a = centre + x * width / r;
                    return foot + axis * along + Around(a) * r;
                });
            }

            // The silk, a few strands curling out of the tip and down.
            Vector3 tip = foot + axis * (young ? length * 1.06f : cobLength * 0.97f);
            for (int k = 0; k < 5; k++)
            {
                float a = yaw + d.Range(-70f, 70f);
                Stem(m, Paint.Silk, Spine.Arc(tip, a, d.Range(0.04f, 0.07f), d.Range(40f, 70f), d.Range(-60f, -30f)), 0.0028f, 0.001f, 4, 8);
            }
        }

        // --- Tomato ----------------------------------------------------------------------------------

        static void Tomato(SmoothMesh m, int stage, Dice d)
        {
            switch (stage)
            {
                case 0:
                    // Seedlings with two seed leaves and the first true leaves opening between them.
                    Sown(d, 3, 0.045f, (at, turn, size) =>
                    {
                        Spine stem = Spine.Arc(at + Vector3.down * 0.01f, 60f + turn, 0.07f * size, 86f, 80f);
                        Stem(m, Paint.TomatoStem, stem, 0.0065f * size, 0.005f * size, 6, 6);
                        for (int s = 0; s < 2; s++)
                            Leaf(m, Paint.Seedling, Spine.Arc(stem.End, 40f + turn + s * 180f, 0.055f * size, 25f, -8f), 0.015f * size, Narrow, 0.25f);
                        Leaflet(m, Paint.Seedling, stem.End, 130f + turn, 0.042f * size, 60f, 30f);
                        Leaflet(m, Paint.TomatoLeaf, stem.End, 310f + turn, 0.034f * size, 70f, 40f);
                    });
                    break;

                case 1:
                    TomatoPlant(m, d, 0.19f, 0.0085f, 6, 0.12f, 0.052f, 0, 0, null, false);
                    break;

                case 2:
                    TomatoPlant(m, d, 0.32f, 0.01f, 7, 0.135f, 0.057f, 2, 3, new[] { Paint.TomatoGreen }, true);
                    break;

                default:
                    TomatoPlant(m, d, 0.38f, 0.011f, 8, 0.145f, 0.06f, 4, 0,
                                new[] { Paint.TomatoRed, Paint.TomatoRed, Paint.TomatoOrange, Paint.TomatoRed }, true);
                    break;
            }
        }

        /// <summary>
        /// A tomato vine: a stem tied to a stake behind it, compound leaves spiralling up it, and on
        /// the grown plants trusses hanging from it - of fruit painted <paramref name="fruit"/>, or of
        /// flowers where <paramref name="flowers"/> is set.
        /// </summary>
        static void TomatoPlant(SmoothMesh m, Dice d, float height, float radius, int leaves, float leafLength, float leaflet,
                                int trusses, int flowers, Paint[] fruit, bool staked)
        {
            Vector3 behind = Horizontal(323f) * 0.05f;
            Spine stem = Spine.Arc(Vector3.down * 0.01f, 323f, height, staked ? 86f : 84f, 88f, 40f);
            Stem(m, Paint.TomatoStem, stem, radius, radius * 0.55f, 8, 12);

            if (staked)
            {
                float stakeHeight = height + 0.1f;
                m.RoundedBox((int)Paint.Stake, behind + Vector3.up * (stakeHeight * 0.5f - 0.03f), new Vector3(0.024f, stakeHeight + 0.06f, 0.024f),
                             0.007f, Quaternion.Euler(0f, 37f, 0f), 2);

                foreach (float t in new[] { 0.35f, 0.75f })
                {
                    Vector3 on = stem.At(t);
                    Vector3 post = new Vector3(behind.x, on.y, behind.z);
                    Stem(m, Paint.Twine, Spine.Line(on + (on - post).normalized * radius * 0.8f, post), 0.0045f, 0.0045f, 6, 2);
                }
            }

            for (int i = 0; i < leaves; i++)
            {
                float t = Mathf.Lerp(0.18f, 0.95f, i / (float)Mathf.Max(1, leaves - 1));
                float yaw = 60f + i * 137.5f + d.Range(-12f, 12f);
                float up = i / (float)Mathf.Max(1, leaves - 1);
                CompoundLeaf(m, d, stem.At(t), yaw, leafLength * Mathf.Lerp(1.1f, 0.75f, up), leaflet * Mathf.Lerp(1f, 0.8f, up),
                             Mathf.Lerp(35f, 60f, up), Mathf.Lerp(-30f, 5f, up));
            }

            // Leaves bunched at the growing tip.
            for (int k = 0; k < 3; k++)
                Leaflet(m, Paint.TomatoLeaf, stem.End, k * 120f + 30f, leaflet * 0.7f, 70f, 40f);

            for (int i = 0; i < trusses; i++)
            {
                float t = Mathf.Lerp(0.4f, 0.72f, i / (float)Mathf.Max(1, trusses - 1));
                float yaw = 143f + (i - (trusses - 1) * 0.5f) * 75f + d.Range(-10f, 10f);
                Truss(m, d, stem.At(t) + Horizontal(yaw) * radius, yaw, fruit, flowers > 0 && i == 0 ? 0 : 3 + (i % 2), i);
            }

            for (int i = 0; i < (flowers > 0 ? 2 : 0); i++)
            {
                float t = 0.78f + i * 0.1f;
                float yaw = 100f + i * 110f;
                Blossoms(m, d, stem.At(t) + Horizontal(yaw) * radius, yaw, flowers);
            }
        }

        static void CompoundLeaf(SmoothMesh m, Dice d, Vector3 at, float yaw, float length, float leaflet, float rise, float tip)
        {
            Spine rachis = Spine.Arc(at, yaw, length, rise, tip);
            Stem(m, Paint.TomatoStem, rachis, 0.0035f, 0.0018f, 5, 8);

            Leaflet(m, Paint.TomatoLeaf, rachis.End, yaw + d.Range(-10f, 10f), leaflet, tip + 5f, tip - 20f);
            foreach (float t in new[] { 0.45f, 0.78f })
                for (int side = -1; side <= 1; side += 2)
                    Leaflet(m, Paint.TomatoLeaf, rachis.At(t), yaw + side * d.Range(55f, 75f), leaflet * Mathf.Lerp(0.7f, 1f, t),
                            d.Range(5f, 20f), d.Range(-35f, -15f));
        }

        static void Leaflet(SmoothMesh m, Paint paint, Vector3 at, float yaw, float length, float rise, float tip) =>
            Leaf(m, paint, Spine.Arc(at, yaw, length, rise, tip), length * 0.42f, Ovate, 0.35f, 0.12f, 2f);

        /// <summary>A truss hanging out and down from the stem, with its fruit clustered along it.</summary>
        static void Truss(SmoothMesh m, Dice d, Vector3 at, float yaw, Paint[] fruit, int count, int index)
        {
            if (fruit == null || count == 0) return;

            Spine stalk = Spine.Arc(at, yaw, 0.075f, 25f, -55f);
            Stem(m, Paint.TomatoStem, stalk, 0.0035f, 0.0025f, 5, 8);

            bool ripe = fruit[0] != Paint.TomatoGreen;
            float size = ripe ? 0.04f : 0.027f;

            for (int k = 0; k < count; k++)
            {
                float t = Mathf.Lerp(0.5f, 1f, k / (float)Mathf.Max(1, count - 1));
                float a = yaw + (k - (count - 1) * 0.5f) * 70f;
                float r = size * Mathf.Lerp(0.9f, 1.05f, d.Value) * (k == count - 1 ? 0.85f : 1f);
                Vector3 hang = stalk.At(t) + Horizontal(a) * r * 0.9f + Vector3.down * r * 0.95f;
                TomatoFruit(m, fruit[(k + index) % fruit.Length], hang, r, d.Range(0f, 360f));
            }
        }

        static void TomatoFruit(SmoothMesh m, Paint paint, Vector3 centre, float r, float turn)
        {
            m.Surface((int)paint, 16, 12, (u, v) =>
            {
                float th = v * Mathf.PI, ph = u * Mathf.PI * 2f;
                float lobe = 1f + 0.045f * Mathf.Cos(ph * 5f) * Mathf.Sin(th);
                float dimple = 0.14f * Mathf.Pow(Mathf.Max(0f, v - 0.82f) / 0.18f, 2f);
                var p = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph) * lobe, -Mathf.Cos(th) * 0.86f - dimple, Mathf.Sin(th) * Mathf.Sin(ph) * lobe);
                return centre + p * r;
            }, wrapU: true);

            // The calyx: a green star over the top.
            Vector3 top = centre + Vector3.up * r * 0.73f;
            for (int k = 0; k < 5; k++)
                Leaf(m, Paint.TomatoStem, Spine.Arc(top, turn + k * 72f, r * 0.8f, 18f, -40f), r * 0.14f, Narrow, 0.2f);
            Stem(m, Paint.TomatoStem, Spine.Line(top, top + Vector3.up * r * 0.4f), r * 0.08f, r * 0.06f, 5, 2);
        }

        /// <summary>A little spray of yellow flowers, nodding.</summary>
        static void Blossoms(SmoothMesh m, Dice d, Vector3 at, float yaw, int count)
        {
            Spine stalk = Spine.Arc(at, yaw, 0.05f, 30f, -20f);
            Stem(m, Paint.TomatoStem, stalk, 0.0028f, 0.002f, 5, 6);

            for (int k = 0; k < count; k++)
            {
                float t = Mathf.Lerp(0.45f, 1f, k / (float)Mathf.Max(1, count - 1));
                Vector3 centre = stalk.At(t) + Horizontal(yaw + (k - 1) * 70f) * 0.01f + Vector3.down * 0.012f;
                Blob(m, Paint.TomatoStem, centre, new Vector3(0.004f, 0.006f, 0.004f), Quaternion.identity, 6, 4);
                float turn = d.Range(0f, 72f);
                for (int p = 0; p < 5; p++)
                    Leaf(m, Paint.Flower, Spine.Arc(centre, turn + p * 72f, 0.017f, -5f, -55f), 0.0055f, Narrow, 0.15f);
                Blob(m, Paint.Flower, centre + Vector3.down * 0.004f, new Vector3(0.0035f, 0.008f, 0.0035f), Quaternion.identity, 6, 4);
            }
        }

        // --- Cabbage ---------------------------------------------------------------------------------

        static void Cabbage(SmoothMesh m, int stage, Dice d)
        {
            switch (stage)
            {
                case 0:
                    Sown(d, 3, 0.05f, (at, turn, size) =>
                    {
                        Spine stem = Spine.Line(at + Vector3.down * 0.01f, at + Vector3.up * 0.03f * size);
                        Stem(m, Paint.CabbageInner, stem, 0.006f * size, 0.0055f * size, 6, 2);
                        for (int s = 0; s < 2; s++)
                            Leaf(m, Paint.Seedling, Spine.Arc(stem.End, 53f + turn + s * 180f, 0.05f * size, 30f, -5f), 0.024f * size, Round, 0.2f);
                        Leaf(m, Paint.CabbageYoung, Spine.Arc(stem.End, 143f + turn, 0.05f * size, 70f, 40f), 0.021f * size, Spoon, 0.35f);
                        Leaf(m, Paint.CabbageYoung, Spine.Arc(stem.End, 323f + turn, 0.04f * size, 74f, 46f), 0.017f * size, Spoon, 0.35f);
                    });
                    break;

                case 1:
                    Rosette(m, d, Paint.CabbageYoung, 8, 0.11f, 0.13f, 0.05f, 52f, 10f, 0.35f, 0.05f);
                    Rosette(m, d, Paint.CabbageInner, 3, 0.06f, 0.07f, 0.03f, 78f, 55f, 0.5f, 0.03f, 60f);
                    break;

                case 2:
                    Rosette(m, d, Paint.CabbageOuter, 9, 0.16f, 0.18f, 0.072f, 45f, 2f, 0.35f, 0.08f);
                    Rosette(m, d, Paint.CabbageInner, 5, 0.115f, 0.125f, 0.052f, 68f, 38f, 0.45f, 0.06f, 30f);
                    Head(m, d, new Vector3(0f, 0.05f, 0f), new Vector3(0.05f, 0.05f, 0.05f), 4);
                    break;

                default:
                    Rosette(m, d, Paint.CabbageOuter, 12, 0.18f, 0.2f, 0.084f, 34f, -6f, 0.4f, 0.1f);
                    Rosette(m, d, Paint.CabbageInner, 6, 0.135f, 0.15f, 0.064f, 62f, 28f, 0.5f, 0.08f, 25f);
                    Head(m, d, new Vector3(0f, 0.076f, 0f), new Vector3(0.092f, 0.082f, 0.092f), 7);
                    break;
            }
        }

        /// <summary>A ring of broad cupped leaves round the middle, turned by the golden angle so none lines up.</summary>
        static void Rosette(SmoothMesh m, Dice d, Paint paint, int count, float minLength, float maxLength, float halfWidth, float rise,
                            float tip, float cup, float wave, float turn = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                float yaw = turn + i * 137.5f + d.Range(-10f, 10f);
                Vector3 at = Ground(yaw, 0.012f) + Vector3.up * 0.004f;
                float length = d.Range(minLength, maxLength);
                Leaf(m, paint, Spine.Arc(at, yaw, length, rise + d.Range(-5f, 5f), tip + d.Range(-6f, 6f), d.Range(-12f, 12f)),
                     halfWidth * d.Range(0.9f, 1.1f), Spoon, cup, wave, 3.5f, 18, 8);
            }
        }

        /// <summary>The heart: a firm round core with leaves wrapped tight over it, overlapping on top.</summary>
        static void Head(SmoothMesh m, Dice d, Vector3 centre, Vector3 radii, int wraps)
        {
            Blob(m, Paint.CabbageCore, centre, radii, Quaternion.identity, 18, 12);

            for (int k = 0; k < wraps; k++)
            {
                float middle = k * 137.5f * Mathf.Deg2Rad;
                float halfWidth = d.Range(0.85f, 1.05f);
                float end = d.Range(0.84f, 0.95f) * Mathf.PI;
                float lift = 1.025f + 0.014f * k;

                m.Surface((int)Paint.CabbageHead, 8, 14, (u, v) =>
                {
                    float x = u * 2f - 1f;
                    float th = Mathf.Lerp(0.3f * Mathf.PI, end, v);
                    float taper = Mathf.Pow(Arch(Mathf.PI * Mathf.Lerp(0.1f, 0.97f, v)), 0.45f);
                    float ph = middle + x * halfWidth * taper;
                    float s = lift + 0.05f * x * x * (0.4f + v);
                    var p = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), -Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    return centre + Vector3.Scale(p, radii) * s;
                });
            }
        }

        /// <summary>
        /// A few seedlings close together, as seed sown thick comes up, so even the first stage covers
        /// its spot: <paramref name="seedling"/> builds one at a point on the soil, turned by some
        /// degrees and at a size (the first full size in the middle, the rest a little smaller round it).
        /// </summary>
        static void Sown(Dice d, int count, float spread, Action<Vector3, float, float> seedling)
        {
            float start = d.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                Vector3 at = i == 0 ? Vector3.zero : Ground(start + i * 137.5f, spread * d.Range(0.75f, 1f));
                seedling(at, d.Range(-30f, 30f), i == 0 ? 1f : d.Range(0.7f, 0.88f));
            }
        }

        // --- Parts -----------------------------------------------------------------------------------

        /// <summary>A grass or corn blade: a strap, narrow where it leaves the stem, tapering to a point.</summary>
        static void Blade(SmoothMesh m, Paint paint, Vector3 at, float yaw, float length, float halfWidth, float rise, float tip,
                          float cup = 0.25f, float wave = 0f) =>
            Leaf(m, paint, Spine.Arc(at, yaw, length, rise, tip), halfWidth, Strap, cup, wave, 4f);

        static float Strap(float v) => (0.45f + 0.55f * Mathf.Sin(Mathf.PI * 0.5f * Mathf.Min(v / 0.3f, 1f))) * Mathf.Pow(1f - v, 0.7f);
        /// <summary>sin(x) kept off zero's far side: sin(pi) comes out a hair below it, and a fractional power of that is NaN.</summary>
        static float Arch(float x) => Mathf.Max(0f, Mathf.Sin(x));

        static float Ovate(float v) => Mathf.Pow(Arch(Mathf.PI * Mathf.Pow(v, 0.75f)), 0.75f);
        static float Narrow(float v) => Mathf.Pow(Arch(Mathf.PI * v), 0.8f) * (1f - 0.3f * v);
        static float Round(float v) => Mathf.Pow(Arch(Mathf.PI * Mathf.Pow(v, 0.8f)), 0.5f);

        /// <summary>A stalk narrow at the base widening into a broad rounded blade: a cabbage leaf.</summary>
        static float Spoon(float v) =>
            Mathf.Lerp(0.12f, 1f, Mathf.SmoothStep(0f, 1f, (v - 0.05f) / 0.45f)) * Mathf.Pow(Mathf.Max(0f, 1f - v * v * v), 0.5f);

        /// <summary>
        /// A leaf along <paramref name="spine"/>, <paramref name="halfWidth"/> either side of it times
        /// <paramref name="shape"/>(v), its edges curled up by <paramref name="cup"/> and rippled by
        /// <paramref name="wave"/>. Its uv runs across (0.5 the midrib) and from base to tip.
        /// </summary>
        static void Leaf(SmoothMesh m, Paint paint, Spine spine, float halfWidth, Func<float, float> shape, float cup, float wave = 0f,
                         float waves = 3f, int along = 16, int across = 6)
        {
            m.Surface((int)paint, across, along, (u, v) =>
            {
                float x = u * 2f - 1f;
                Vector3 tangent = spine.Tangent(v), side = spine.Side(v);
                Vector3 up = Vector3.Cross(tangent, side).normalized;
                float w = halfWidth * shape(v);
                float lift = cup * x * x + wave * x * x * Mathf.Sin((v * waves + x * 0.35f) * Mathf.PI * 2f);
                return spine.At(v) + side * (x * w) + up * (w * lift);
            });
        }

        /// <summary>A round stem along <paramref name="spine"/>, tapering from <paramref name="r0"/> to <paramref name="r1"/>.</summary>
        static void Stem(SmoothMesh m, Paint paint, Spine spine, float r0, float r1, int around, int along)
        {
            m.Surface((int)paint, around, along, (u, v) =>
            {
                Vector3 tangent = spine.Tangent(v), side = spine.Side(v);
                Vector3 other = Vector3.Cross(side, tangent).normalized;
                float a = u * Mathf.PI * 2f;
                return spine.At(v) + (side * Mathf.Cos(a) + other * Mathf.Sin(a)) * Mathf.Lerp(r0, r1, v);
            }, wrapU: true);
        }

        /// <summary>An ellipsoid, its uv running round it and from its bottom (v 0) to its top.</summary>
        static void Blob(SmoothMesh m, Paint paint, Vector3 centre, Vector3 radii, Quaternion rotation, int around, int rings)
        {
            m.Surface((int)paint, around, rings, (u, v) =>
            {
                float th = v * Mathf.PI, ph = u * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), -Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                return centre + rotation * Vector3.Scale(p, radii);
            }, wrapU: true);
        }

        /// <summary>A point on the soil <paramref name="distance"/> from the middle, toward <paramref name="yaw"/>.</summary>
        static Vector3 Ground(float yaw, float distance) => Horizontal(yaw) * distance;

        static Vector3 Horizontal(float yaw) => Direction(yaw, 0f);

        /// <summary>Heading <paramref name="yaw"/> degrees round from +Z toward +X, <paramref name="elevation"/> degrees above level.</summary>
        static Vector3 Direction(float yaw, float elevation)
        {
            float y = yaw * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(e) * Mathf.Sin(y), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Cos(y));
        }

        /// <summary>Level, and square to <paramref name="yaw"/>: across a leaf heading that way.</summary>
        static Vector3 Across(float yaw)
        {
            float y = yaw * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(y), 0f, -Mathf.Sin(y));
        }

        /// <summary>
        /// The line a stem or a leaf follows: points, and the level direction across it at each, which
        /// stays defined when the line stands straight up (where "across" from the tangent would not be).
        /// </summary>
        sealed class Spine
        {
            readonly Vector3[] points;
            readonly Vector3[] sides;

            Spine(Vector3[] points, Vector3[] sides)
            {
                this.points = points;
                this.sides = sides;
            }

            public Vector3 End => points[points.Length - 1];

            /// <summary>
            /// From <paramref name="start"/>, heading <paramref name="yaw"/>, rising at <paramref name="rise"/>
            /// degrees and bending evenly to <paramref name="tip"/> by its end: 90 is straight up, negative
            /// droops. <paramref name="turn"/> swings the heading round by its end.
            /// </summary>
            public static Spine Arc(Vector3 start, float yaw, float length, float rise, float tip, float turn = 0f, int steps = 24)
            {
                var p = new Vector3[steps + 1];
                var s = new Vector3[steps + 1];
                p[0] = start;
                s[0] = Across(yaw);

                for (int i = 1; i <= steps; i++)
                {
                    float t = (i - 0.5f) / steps;
                    p[i] = p[i - 1] + Direction(yaw + turn * t, Mathf.Lerp(rise, tip, t)) * (length / steps);
                    s[i] = Across(yaw + turn * i / steps);
                }

                return new Spine(p, s);
            }

            public static Spine Line(Vector3 from, Vector3 to)
            {
                Vector3 d = to - from;
                float length = d.magnitude;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float elevation = Mathf.Asin(Mathf.Clamp(d.y / Mathf.Max(length, 1e-6f), -1f, 1f)) * Mathf.Rad2Deg;
                return Arc(from, yaw, length, elevation, elevation, 0f, 4);
            }

            /// <summary>The point <paramref name="t"/> of the way along, on a smooth curve through the points.</summary>
            public Vector3 At(float t)
            {
                int last = points.Length - 1;
                float f = Mathf.Clamp01(t) * last;
                int i = Mathf.Min((int)f, last - 1);
                float k = f - i, k2 = k * k, k3 = k2 * k;

                Vector3 p0 = points[Mathf.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(i + 2, last)];
                return 0.5f * (2f * p1 + (p2 - p0) * k + (2f * p0 - 5f * p1 + 4f * p2 - p3) * k2 + (3f * p1 - p0 - 3f * p2 + p3) * k3);
            }

            public Vector3 Tangent(float t)
            {
                const float e = 0.01f;
                Vector3 d = At(Mathf.Min(t + e, 1f)) - At(Mathf.Max(t - e, 0f));
                return d.sqrMagnitude > 1e-14f ? d.normalized : Vector3.up;
            }

            /// <summary>Across the line at <paramref name="t"/>, square to its tangent.</summary>
            public Vector3 Side(float t)
            {
                int last = sides.Length - 1;
                float f = Mathf.Clamp01(t) * last;
                int i = Mathf.Min((int)f, last - 1);
                Vector3 s = Vector3.Lerp(sides[i], sides[i + 1], f - i);
                Vector3 tangent = Tangent(t);
                return (s - tangent * Vector3.Dot(s, tangent)).normalized;
            }
        }

        /// <summary>Seeded, so each stage is the same plant every time it is built.</summary>
        sealed class Dice
        {
            readonly System.Random random;

            public Dice(int seed) => random = new System.Random(seed);

            public float Value => (float)random.NextDouble();

            public float Range(float min, float max) => min + Value * (max - min);
        }
    }
}
