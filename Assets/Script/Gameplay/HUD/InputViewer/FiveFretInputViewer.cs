using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.HUD
{
    public class FiveFretInputViewer : BaseInputViewer
    {
        public override void OnInput(GameInput input)
        {
            int index;
            switch ((GuitarAction) input.Action)
            {
                case GuitarAction.GreenFret:
                case GuitarAction.RedFret:
                case GuitarAction.YellowFret:
                case GuitarAction.BlueFret:
                case GuitarAction.OrangeFret:
                    index = input.Action;
                    break;
                case GuitarAction.StrumUp:
                case GuitarAction.StrumDown:
                    index = input.Action - 1;
                    break;
                default:
                    return;
            }

            _buttons[index].UpdatePressState(input.Button, input.Time);
        }

        public override void SetColors(ColorProfile colorProfile)
        {
            _buttons[0].ButtonColor = colorProfile.FiveFretGuitar.GreenNote.ToUnityColor();
            _buttons[1].ButtonColor = colorProfile.FiveFretGuitar.RedNote.ToUnityColor();
            _buttons[2].ButtonColor = colorProfile.FiveFretGuitar.YellowNote.ToUnityColor();
            _buttons[3].ButtonColor = colorProfile.FiveFretGuitar.BlueNote.ToUnityColor();
            _buttons[4].ButtonColor = colorProfile.FiveFretGuitar.OrangeNote.ToUnityColor();

            _buttons[5].ButtonColor = colorProfile.FiveFretGuitar.OpenNote.ToUnityColor();
            _buttons[6].ButtonColor = colorProfile.FiveFretGuitar.OpenNote.ToUnityColor();
        }
    }
}