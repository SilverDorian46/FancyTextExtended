using System.Collections;

namespace Celeste.Mod.FancyTextExtended;

[CustomEntity("FancyTextExtended/TestTalker")]
public class TestTalker : Entity
{
    public const string DialogID = "fancyTextExtended_testDialog";

    private Level level = default!;

    private Player? player;
    private BadelineDummy? baddy;

    private Coroutine? talkRoutine;

    public TestTalker(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        Add(new TalkComponent(new Rectangle(-8, -8, 16, 8), new Vector2(0, -24), OnTalk));
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);

        level = (scene as Level)!;
    }

    private void OnTalk(Player player)
    {
        level.StartCutscene(OnTalkEnd);
        Add(talkRoutine = new Coroutine(Talk(player)));
    }

    private IEnumerator Talk(Player player)
    {
        this.player = player;
        this.player.StateMachine.State = Player.StDummy;
        this.player.StateMachine.Locked = true;

        yield return player.DummyWalkToExact((int)X);
        player.Facing = Facings.Right;

        yield return Textbox.Say(DialogID, BadelineComeOut);

        yield return BadelineRejoin();

        level.EndCutscene();
        OnTalkEnd(level);
    }

    private IEnumerator BadelineComeOut()
    {
        Audio.Play(SFX.char_bad_maddy_split, player!.Position);
        level.Add(baddy = new(player.Center));
        level.Displacement.AddBurst(baddy.Center, 0.5f, 8, 32, 0.5f);
        player.Dashes = 1;
        baddy.Sprite.Scale.X = -1;
        yield return baddy.FloatTo(player.Center + new Vector2(18, -10), -1, faceDirection: false);
        yield return 0.2f;

        yield return null;
    }

    private IEnumerator BadelineRejoin()
    {
        if (baddy is null)
            yield break;

        Audio.Play(SFX.char_bad_maddy_join_quick, baddy.Position);
        Vector2 from = baddy.Position;
        for (float p = 0; p < 1; p += Engine.DeltaTime / 0.25f)
        {
            baddy.Position = Vector2.Lerp(from, player!.Position, Ease.CubeIn(p));
            yield return null;
        }

        level.Displacement.AddBurst(baddy.Center, 0.5f, 8, 32, 0.5f);
        baddy.RemoveSelf();
        yield return 0.1f;
    }

    private void OnTalkEnd(Level level)
    {
        if (player is not null)
        {
            player.StateMachine.Locked = false;
            player.StateMachine.State = Player.StNormal;
        }

        baddy?.RemoveSelf();

        if (talkRoutine is not null)
        {
            talkRoutine.Cancel();
            talkRoutine.RemoveSelf();
        }
    }
}
