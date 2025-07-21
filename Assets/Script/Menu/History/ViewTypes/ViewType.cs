using YARG.Core.Game;
using YARG.Core.Replays;
using YARG.Core.Song;
using YARG.Menu.ListMenu;
using YARG.Replays;

namespace YARG.Menu.History
{
    public abstract class ViewType : BaseViewType
    {
        public struct GameInfo
        {
            public int BandScore;
            public StarAmount BandStars;
        }

        public abstract bool UseFullContainer { get; }

        public virtual void ViewClick()
        {

        }

        public virtual void PlayWithReplayClick()
        {

        }

        public virtual void Shortcut1()
        {

        }

        public virtual void Shortcut2()
        {

        }

        public virtual void Shortcut3()
        {

        }

        public virtual GameInfo? GetGameInfo()
        {
            return null;
        }

        protected static void PlayWithReplay(ReplayInfo replay, SongEntry song)
        {
            GlobalVariables.State = PersistentState.Default;

            GlobalVariables.State.CurrentSong = song;
            GlobalVariables.State.CurrentReplay = replay;
            GlobalVariables.State.PlayingWithReplay = true;

            // GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
            MenuManager.Instance.PushMenu(MenuManager.Menu.DifficultySelect);
        }

        protected static void LoadIntoReplay(ReplayInfo replay, SongEntry song)
        {
            GlobalVariables.State = PersistentState.Default;

            GlobalVariables.State.CurrentSong = song;
            GlobalVariables.State.CurrentReplay = replay;

            // TODO: Store selected song speed in replays
            // GlobalVariables.State.SongSpeed = replay.

            GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
        }
    }
}