using System.Collections.Generic;
using YARG.Menu.ListMenu;
using YARG.Scores;

namespace YARG.Menu.History
{
    public class HistoryMenu : ListMenu<ViewType, HistoryView>
    {
        protected override int ExtraListViewPadding => 5;

        protected override List<ViewType> CreateViewList()
        {
            var list = new List<ViewType>();

            foreach (var record in ScoreContainer.GetAllGameRecords())
            {
                list.Add(new GameRecordViewType(record));
            }

            return list;
        }
    }
}