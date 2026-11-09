using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

namespace TailscaleCapsule;
public sealed partial class MainWindow
{
    DateTimeOffset lastUpdateCheck=DateTimeOffset.MinValue;
    bool checkingUpdates;
    async Task CheckUpdatesAsync(bool manual)
    {
        if(checkingUpdates||DemoMode||exiting)return;
        checkingUpdates=true;lastUpdateCheck=DateTimeOffset.UtcNow;
        try
        {
            var update=await ReleaseUpdateClient.CheckAsync(lifetime.Token);
            if(update==null){if(manual)Toast("目前已是最新版本 · "+ReleaseUpdateClient.CurrentVersion);return;}
            var action=new Button{Content=L("查看更新"),CornerRadius=new(8),FontFamily=new("MiSans TC")};
            action.Click+=async (_,_)=>
            {
                if(update.Download.Length==0){Process.Start(new ProcessStartInfo(update.Page){UseShellExecute=true});return;}
                action.IsEnabled=false;notice.Message=L("正在下載並驗證更新…");
                try
                {
                    string installer=await ReleaseUpdateClient.DownloadAsync(update,lifetime.Token);
                    var start=new ProcessStartInfo(installer){UseShellExecute=true};start.ArgumentList.Add("/VERYSILENT");start.ArgumentList.Add("/SUPPRESSMSGBOXES");start.ArgumentList.Add("/NORESTART");
                    Process.Start(start);ExitApp();
                }
                catch(OperationCanceledException){}
                catch(Exception ex){App.Log("Update: "+ex.Message);Toast("更新未完成，請稍後再試。",true,false);action.IsEnabled=true;}
            };
            Toast("有新版本 "+update.Version,false,false);notice.ActionButton=action;
        }
        catch(OperationCanceledException){if(manual&&!exiting)Toast("檢查更新逾時，請稍後再試。",true);}
        catch(Exception ex){App.Log("Update check: "+ex.Message);if(manual)Toast("暫時無法檢查更新。",true);}
        finally{checkingUpdates=false;}
    }
}
