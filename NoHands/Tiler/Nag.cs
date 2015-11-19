using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.System.Threading;
using Windows.UI.Notifications;

using Tiler.NotificationsExtensions;
using Tiler.NotificationsExtensions.BadgeContent;
using Tiler.NotificationsExtensions.TileContent;

namespace Tiler
{
    public sealed class Nag : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            BackgroundTaskDeferral _deferral = taskInstance.GetDeferral();

            // Update every 15 minutes the counter and add the badge counter value which is shown on tile and lock screen
            BadgeNumericNotificationContent badgeContent = new BadgeNumericNotificationContent(1); // put 1 as a number of notifications
            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(badgeContent.CreateNotification());

            // Update the wide tile and the lock screen at the same time
            ITileWide310x150SmallImageAndText03 tileContent = TileContentFactory.CreateTileWide310x150SmallImageAndText03();
            tileContent.TextBodyWrap.Text = "Haven't seen you for a while, come and take funny photos!";
            tileContent.Image.Src = "ms-appx:///Assets/Wide310x150Logo.scale-200.png";
            tileContent.RequireSquare150x150Content = false;
            TileUpdateManager.CreateTileUpdaterForApplication().Update(tileContent.CreateNotification());

            _deferral.Complete();
        }
    }
}
