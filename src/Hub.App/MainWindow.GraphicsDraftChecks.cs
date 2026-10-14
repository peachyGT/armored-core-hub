using System.Windows;
using Hub.Core;

namespace Hub.App;

public partial class MainWindow
{
    private void CheckGraphicsDrafts(List<string> checks)
    {
        var saved=store.Load();var previousGame=(GameList.SelectedItem as GameRow)?.Id;
        try
        {
            GameList.SelectedItem=rows.Single(r=>r.Id=="ac4");
            const string key="Video/Write Color Buffers";
            const string unrelated="Miscellaneous/Start games in fullscreen mode";
            Current.AdvancedSettings.Clear();advancedContext="";graphicsContext="";RefreshDetail();
            var original=graphicsEditors[key].Text;
            graphicsEditors[key].Text="not-a-boolean";RefreshDetail();
            if(!HasGraphicsDraft||graphicsEditors[key].Text!="not-a-boolean")throw new Exception("Routine settings refresh discarded the graphics draft");
            if(TrySaveGraphicsAdvanced(false)||Current.AdvancedSettings.Count!=0||store.Load().Selections[edition!.Id].AdvancedSettings.Count!=0)throw new Exception("Invalid graphics draft saved partially");
            DiscardGraphicsDraft();if(HasGraphicsDraft||graphicsEditors[key].Text!=original)throw new Exception("Graphics draft discard failed");
            graphicsEditors[key].Text=original=="true"?"false":"true";
            AdvancedText.Text=AdvancedText.Text.Replace(unrelated+"=true",unrelated+"=false");
            if(!TrySaveGraphicsAdvanced(false)||HasGraphicsDraft||Current.AdvancedSettings[key]==original||!AdvancedText.Text.Contains(unrelated+"=false")||AdvancedText.Text==advancedSavedText)
                throw new Exception("Graphics save lost a separate advanced draft or did not persist");
            if(!TrySaveAdvanced(false)||Current.AdvancedSettings.GetValueOrDefault(unrelated)!="false"||graphicsEditors[key].Text!=Current.AdvancedSettings[key])throw new Exception("Advanced save failed to synchronize graphics editors");
            // Both editors are editable independently; one must not overwrite untouched keys.
            graphicsEditors[key].Text=original;
            AdvancedText.Text=AdvancedText.Text.Replace(unrelated+"=false",unrelated+"=true");
            if(!TrySaveAdvanced(false)||!HasGraphicsDraft||graphicsEditors[key].Text!=original||!TrySaveGraphicsAdvanced(false)||Current.AdvancedSettings.ContainsKey(key)||Current.AdvancedSettings.ContainsKey(unrelated))
                throw new Exception("Saving both editor drafts overwrote an unrelated setting");
            checks.Add("Graphics drafts survive routine refresh; invalid edits save nothing; save/discard and raw Advanced edits remain synchronized without losing unrelated keys");
        }
        finally
        {
            AdvancedText.Text=advancedSavedText;DiscardGraphicsDraft();
            state.Selections=saved.Selections;Save();advancedContext="";graphicsContext="";
            graphicsEditors.Clear();graphicsSavedValues.Clear();
            GameList.SelectedItem=rows.FirstOrDefault(r=>r.Id==previousGame)??rows.First();RefreshDetail();
        }
    }
}
