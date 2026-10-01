namespace Pepin;

/// <summary>Outil de dev : `Pepin.exe --sheet out.png` dessine toutes les poses sur une planche.</summary>
public static class SheetRenderer
{
    public static void Render(string path)
    {
        var poses = new List<Action<Visual>>
        {
            v => { },                                                            // 1 neutre
            v => { v.Blink = true; },                                            // 2 clignement
            v => { v.LookX = -1; v.LookY = -1; },                                // 3 regarde en haut à gauche
            v => { v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin; v.Blush = true; v.Add(FxKind.Hearts, .3f, 2); v.Tint = Pink(); v.TintAmount = .25f; },
            v => { v.Eyes = Eyes.Hearts; v.Mouth = Mouth.Grin; v.Blush = true; }, // 5 amoureux
            v => { v.Eyes = Eyes.Closed; v.Mouth = Mouth.ChewA; v.HeadOut = .6f; v.LegsTuck = 3; v.Add(FxKind.Zzz, .5f); v.Add(FxKind.Bubble, 0, .8f); v.Tint = Pale(); v.TintAmount = .35f; },
            v => { v.Eyes = Eyes.Closed; v.Mouth = Mouth.Smile; v.HeadOut = .6f; v.LegsTuck = 3; v.DreamOf = Snack.Strawberry; v.Add(FxKind.Dream); },
            v => { v.Eyes = Eyes.Angry; v.Mouth = Mouth.Zigzag; v.Add(FxKind.Anger); v.Add(FxKind.Steam, .3f); v.Flush = .6f; },
            v => { v.Eyes = Eyes.Wide; v.Mouth = Mouth.Oh; v.Add(FxKind.Exclaim); },
            v => { v.Eyes = Eyes.Spiral; v.Mouth = Mouth.Zigzag; v.Add(FxKind.Stars, .2f); },
            v => { v.Eyes = Eyes.HalfLid; v.Mouth = Mouth.Pout; v.FacingRight = false; v.Add(FxKind.Grumble); },
            v => { v.Eyes = Eyes.Closed; v.Mouth = Mouth.Yawn; v.HeadDy = -1; },  // 12 bâille
            v => { v.Eyes = Eyes.Squint; v.Mouth = Mouth.Smile; v.HeadDx = 2; v.HeadDy = -2; }, // 13 s'étire
            v => { v.Eyes = Eyes.Happy; v.Mouth = Mouth.ChewB; v.Food = Item.Salade; v.Add(FxKind.Food, 0, .6f); v.Add(FxKind.Crumbs, .3f); },
            v => { v.Eyes = Eyes.Normal; v.Mouth = Mouth.ChewA; v.Food = Item.Fraise; v.FacingRight = false; v.Add(FxKind.Food, 0, 1); },
            v => { v.Eyes = Eyes.Happy; v.Mouth = Mouth.Smile; v.Add(FxKind.Notes, .4f, 2); },
            v => { v.Eyes = Eyes.Determined; v.Mouth = Mouth.Smile; v.BodyDy = 1; v.LegsTuck = 1; },
            v => { v.Eyes = Eyes.Determined; v.Mouth = Mouth.Bite; v.ShadowZ = 6; },   // 18 bondit
            v => { v.Eyes = Eyes.Wide; v.Mouth = Mouth.Oh; v.LegsDangle = true; v.LegPhase = 1; v.NoShadow = true; v.Add(FxKind.Sweat, .5f); },
            v => { v.InShell = true; v.SpinFrame = 0; v.Add(FxKind.Speed, .3f); },
            v => { v.InShell = true; v.SpinFrame = 1; },
            v => { v.InShell = true; v.SpinFrame = 2; },
            v => { v.HeadOut = .35f; v.Eyes = Eyes.Wide; v.LegsTuck = 3; },        // 23 jette un œil
            v => { v.LegPhase = 1; },                                              // 24-27 marche
            v => { v.LegPhase = 2; },
            v => { v.LegPhase = 3; v.FacingRight = false; },
            v => { v.Eyes = Eyes.Normal; v.Mouth = Mouth.Tongue; v.Add(FxKind.Question); },
            v => { v.Eyes = Eyes.HalfLid; v.Mouth = Mouth.Frown; v.LegsTuck = 3; v.Add(FxKind.Anger); }, // réveil grognon
            v => { v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin; v.Add(FxKind.Dust, .5f); },
            v => { v.Eyes = Eyes.Normal; v.Mouth = Mouth.Smile; v.LegsTuck = 3; v.Eyes = Eyes.HalfLid; },
            v => { v.Eyes = Eyes.Closed; v.Mouth = Mouth.Smile; v.HeadOut = .6f; v.LegsTuck = 3; v.Add(FxKind.Hearts, .5f, 1); },
            v => { v.Eyes = Eyes.Happy; v.Mouth = Mouth.Tongue; v.ShadowZ = 3; v.Add(FxKind.Notes, .1f, 1); },
        };

        const int cols = 6, pad = 2;
        int rows = (poses.Count + cols - 1) / cols + 1;
        var sheet = new PixelCanvas(cols * (TurtleArt.CW + pad), rows * (TurtleArt.CH + pad));
        var one = new PixelCanvas(TurtleArt.CW, TurtleArt.CH);
        var vis = new Visual();
        for (int i = 0; i < poses.Count; i++)
        {
            vis.Reset(); vis.FacingRight = true;
            vis.Tint = 0; vis.TintAmount = 0; vis.DreamOf = Snack.Lettuce; vis.Food = Item.Salade;
            poses[i](vis);
            TurtleArt.Draw(one, vis);
            one.BlitTo(sheet, (i % cols) * (TurtleArt.CW + pad), (i / cols) * (TurtleArt.CH + pad));
        }
        // icône en bas à droite
        var icon = new PixelCanvas(16, 16);
        TurtleArt.DrawIcon(icon);
        icon.BlitTo(sheet, (cols - 1) * (TurtleArt.CW + pad), (rows - 1) * (TurtleArt.CH + pad));
        sheet.SavePng(path, 4, PixelCanvas.Rgb(236, 240, 245));
    }

    /// <summary>`--frames dossier` : images transparentes nommées (icône, page de téléchargement).</summary>
    public static void Frames(string dir)
    {
        Directory.CreateDirectory(dir);
        var frames = new (string name, Action<Visual> set)[]
        {
            ("neutre", v => { }),
            ("cligne", v => { v.Blink = true; }),
            ("marche1", v => { v.LegPhase = 1; }),
            ("marche3", v => { v.LegPhase = 3; }),
            ("calin", v => { v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin; v.Blush = true; v.HeadDy = 1; v.Add(FxKind.Hearts, .3f, 2); }),
            ("dort", v => { v.Eyes = Eyes.Closed; v.HeadOut = .6f; v.LegsTuck = 3; v.Add(FxKind.Zzz, .5f); v.Add(FxKind.Bubble, 0, .8f); v.Tint = Pale(); v.TintAmount = .3f; }),
            ("reve", v => { v.Eyes = Eyes.Closed; v.HeadOut = .6f; v.LegsTuck = 3; v.DreamOf = Snack.Strawberry; v.Add(FxKind.Dream); v.Tint = Pale(); v.TintAmount = .3f; }),
            ("grignote", v => { v.Eyes = Eyes.Happy; v.Mouth = Mouth.ChewB; v.Food = Item.Fraise; v.Add(FxKind.Food, 0, .6f); v.Add(FxKind.Crumbs, .3f); }),
            ("colere", v => { v.Eyes = Eyes.Angry; v.Mouth = Mouth.Zigzag; v.Flush = .7f; v.Add(FxKind.Anger); v.Add(FxKind.Steam, .3f); }),
            ("sonne", v => { v.Eyes = Eyes.Spiral; v.Mouth = Mouth.Zigzag; v.Add(FxKind.Stars, .2f); }),
            ("boude", v => { v.Eyes = Eyes.HalfLid; v.Mouth = Mouth.Pout; v.LegsTuck = 2; v.Add(FxKind.Grumble); }),
            ("attaque", v => { v.Eyes = Eyes.Determined; v.Mouth = Mouth.Bite; v.ShadowZ = 6; v.LegPhase = 1; }),
            ("carapace", v => { v.InShell = true; v.SpinFrame = 1; v.Add(FxKind.Speed, .3f); }),
        };
        var one = new PixelCanvas(TurtleArt.CW, TurtleArt.CH);
        var vis = new Visual();
        foreach (var (name, set) in frames)
        {
            vis.Reset(); vis.FacingRight = true; vis.Tint = 0; vis.TintAmount = 0;
            set(vis);
            TurtleArt.Draw(one, vis);
            one.SavePng(Path.Combine(dir, name + ".png"), 4, 0);
        }
        var icon = new PixelCanvas(16, 16);
        TurtleArt.DrawIcon(icon);
        icon.SavePng(Path.Combine(dir, "icone-256.png"), 16, 0);

        // souvenirs (pages carnet du serveur) : 8×8 px logiques, ×6
        Directory.CreateDirectory(Path.Combine(dir, "items"));
        var it = new PixelCanvas(8, 8);
        foreach (Item item in Enum.GetValues<Item>())
        {
            it.Clear();
            Glyphs.Draw(it, Glyphs.ForItem(item), 4, 4);
            it.SavePng(Path.Combine(dir, "items", Items.Id(item) + ".png"), 6, 0);
        }
    }

    static uint Pink() => PixelCanvas.Rgb(255, 150, 190);
    static uint Pale() => PixelCanvas.Rgb(160, 190, 200);
}
