/*
 UsageWidgetProvider.cs
 RunnerDuck.App

 Created by duks93 on 2026/10/06.
 Copyright 2026 Kyome22 (Takuto Nakamura)
 Modified for RunnerDuck by duks93, 2026.

 Licensed under the Apache License, Version 2.0 (the "License");
 you may not use this file except in compliance with the License.
 You may obtain a copy of the License at

 http://www.apache.org/licenses/LICENSE-2.0

 Unless required by applicable law or agreed to in writing, software
 distributed under the License is distributed on an "AS IS" BASIS,
 WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 See the License for the specific language governing permissions and
 limitations under the License.
 */

using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Providers;

namespace RunnerDuck.App.Widget;

[ComVisible(true)]
[ComDefaultInterface(typeof(IWidgetProvider))]
[Guid("17983EE6-450D-49C2-9D7C-E0E702DE8E97")]
public sealed partial class UsageWidgetProvider : IWidgetProvider
{
    private static UsageWidgetController Controller => UsageWidgetController.Shared;

    public UsageWidgetProvider()
    {
        Controller.RecoverWidgets();
    }

    public void CreateWidget(WidgetContext widgetContext) => Controller.Create(widgetContext.Id);

    public void DeleteWidget(string widgetId, string customState) => Controller.Delete(widgetId);

    public void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs) =>
        Controller.InvokeAction(actionInvokedArgs.WidgetContext.Id, actionInvokedArgs.Verb);

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs) =>
        Controller.Push(contextChangedArgs.WidgetContext.Id);

    public void Activate(WidgetContext widgetContext) => Controller.Activate(widgetContext.Id);

    public void Deactivate(string widgetId) => Controller.Deactivate(widgetId);
}
