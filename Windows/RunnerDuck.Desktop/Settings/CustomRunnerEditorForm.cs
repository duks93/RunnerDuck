/*
 CustomRunnerEditorForm.cs
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

using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using RunnerDuck.App.Properties;
using RunnerDuck.App.Tray;
using RunnerDuck.Core;
using FormsTimer = System.Windows.Forms.Timer;

namespace RunnerDuck.App.Settings;

internal sealed class CustomRunnerEditorForm : Form
{
    private readonly ISettingsHost host;
    private readonly TextBox nameTextBox = new();
    private readonly RadioButton monochromeButton = new() { Text = Strings.Monochrome, AutoSize = true, Checked = true };
    private readonly RadioButton colorButton = new() { Text = Strings.Color, AutoSize = true };
    private readonly FlowLayoutPanel framesPanel = new() { AutoScroll = true, WrapContents = false, AllowDrop = true, BorderStyle = BorderStyle.FixedSingle };
    private readonly Label dropHint = new() { Text = Strings.DropFramesHint, AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly PictureBox previewBox = new() { SizeMode = PictureBoxSizeMode.CenterImage, BorderStyle = BorderStyle.FixedSingle };
    private readonly TrackBar speedTrackBar = new() { Minimum = 1, Maximum = 3, Value = 1, TickStyle = TickStyle.None };
    private readonly Button addButton = new() { Text = Strings.Add, AutoSize = true };
    private readonly FormsTimer previewTimer = new();
    private readonly List<FrameImage> frames = [];
    private Bitmap? previewImage;
    private FrameImage? selectedFrame;
    private int previewIndex;

    public CustomRunnerEditorForm(ISettingsHost host)
    {
        this.host = host;
        Text = Strings.CustomRunnerEditor;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(LogicalToDeviceUnits(520), LogicalToDeviceUnits(520));

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(LogicalToDeviceUnits(16)),
            AutoScroll = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        nameTextBox.Dock = DockStyle.Fill;
        nameTextBox.TextChanged += (_, _) => UpdateAddButton();
        AddRow(layout, Strings.RunnerName, nameTextBox);

        var modePanel = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
        modePanel.Controls.AddRange([monochromeButton, colorButton]);
        monochromeButton.CheckedChanged += (_, _) => RenderPreview();
        AddRow(layout, Strings.RenderingMode, modePanel);

        framesPanel.Height = LogicalToDeviceUnits(96);
        framesPanel.Dock = DockStyle.Fill;
        framesPanel.Controls.Add(dropHint);
        framesPanel.DragEnter += (_, arguments) =>
            arguments.Effect = arguments.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        framesPanel.DragDrop += (_, arguments) =>
        {
            if (arguments.Data?.GetData(DataFormats.FileDrop) is string[] paths)
            {
                AddFrames(paths.Where(path => Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => Path.GetFileName(path), NaturalStringComparer.Instance));
            }
        };
        var frameButtons = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
        var addFrameButton = new Button { Text = Strings.AddFrame, AutoSize = true };
        addFrameButton.Click += (_, _) => ChooseFrames();
        var deleteFrameButton = new Button { Text = Strings.DeleteFrame, AutoSize = true };
        deleteFrameButton.Click += (_, _) => DeleteSelectedFrame();
        frameButtons.Controls.AddRange([addFrameButton, deleteFrameButton]);
        var framesColumn = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, Margin = Padding.Empty };
        framesColumn.Controls.Add(new Label { Text = Strings.FrameRequirements, AutoSize = true, ForeColor = SystemColors.GrayText });
        framesColumn.Controls.Add(framesPanel);
        framesColumn.Controls.Add(frameButtons);
        AddRow(layout, Strings.Frames, framesColumn);

        previewBox.Size = new Size(LogicalToDeviceUnits(160), LogicalToDeviceUnits(72));
        speedTrackBar.Width = LogicalToDeviceUnits(160);
        speedTrackBar.ValueChanged += (_, _) => previewTimer.Interval = PreviewInterval();
        var previewColumn = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Margin = Padding.Empty };
        previewColumn.Controls.AddRange([previewBox, new Label { Text = Strings.PreviewSpeed, AutoSize = true }, speedTrackBar]);
        AddRow(layout, Strings.Preview, previewColumn);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(LogicalToDeviceUnits(12)) };
        var cancelButton = new Button { Text = Strings.Cancel, AutoSize = true, DialogResult = DialogResult.Cancel };
        addButton.Click += (_, _) => AddRunner();
        buttons.Controls.AddRange([addButton, cancelButton]);
        AcceptButton = addButton;
        CancelButton = cancelButton;

        Controls.Add(layout);
        Controls.Add(buttons);
        previewTimer.Interval = PreviewInterval();
        previewTimer.Tick += (_, _) => AdvancePreview();
        previewTimer.Start();
        UpdateAddButton();
    }

    public Runner? AddedRunner { get; private set; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            previewTimer.Dispose();
            previewImage?.Dispose();
            frames.ForEach(frame => frame.Dispose());
        }
        base.Dispose(disposing);
    }

    private void AddRow(TableLayoutPanel layout, string title, Control content)
    {
        layout.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new Padding(0, LogicalToDeviceUnits(6), LogicalToDeviceUnits(12), LogicalToDeviceUnits(12)) });
        content.Margin = new Padding(0, LogicalToDeviceUnits(3), 0, LogicalToDeviceUnits(12));
        layout.Controls.Add(content);
    }

    private void ChooseFrames()
    {
        using var dialog = new OpenFileDialog { Filter = "PNG (*.png)|*.png", Multiselect = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            AddFrames(dialog.FileNames);
        }
    }

    private void AddFrames(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!CustomRunnerRules.CanAddFrame(frames.Count))
            {
                ShowError(CustomRunnerError.FrameLimitExceeded);
                break;
            }
            if (FrameImage.Load(path) is not { } frame)
            {
                ShowError(CustomRunnerError.InvalidFrameImage);
                break;
            }
            frames.Add(frame);
        }
        RebuildFrameThumbnails();
    }

    private void DeleteSelectedFrame()
    {
        if (selectedFrame is not { } frame)
        {
            return;
        }
        var index = frames.IndexOf(frame);
        frames.RemoveAt(index);
        frame.Dispose();
        selectedFrame = frames.Count == 0 ? null : frames[Math.Min(index, frames.Count - 1)];
        RebuildFrameThumbnails();
    }

    private void RebuildFrameThumbnails()
    {
        framesPanel.SuspendLayout();
        framesPanel.Controls.Clear();
        if (frames.Count == 0)
        {
            framesPanel.Controls.Add(dropHint);
        }
        foreach (var frame in frames)
        {
            var thumbnail = new PictureBox
            {
                Image = frame.Bitmap,
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(LogicalToDeviceUnits(Math.Max(36, frame.Bitmap.Width)), LogicalToDeviceUnits(48)),
                BorderStyle = frame == selectedFrame ? BorderStyle.Fixed3D : BorderStyle.None,
                BackColor = frame == selectedFrame ? SystemColors.Highlight : Color.Transparent,
                Margin = new Padding(LogicalToDeviceUnits(4)),
                Cursor = Cursors.Hand,
            };
            thumbnail.Click += (_, _) =>
            {
                selectedFrame = frame;
                RebuildFrameThumbnails();
            };
            framesPanel.Controls.Add(thumbnail);
        }
        framesPanel.ResumeLayout();
        previewIndex = 0;
        RenderPreview();
        UpdateAddButton();
    }

    private void AdvancePreview()
    {
        if (frames.Count == 0)
        {
            return;
        }
        previewIndex = (previewIndex + 1) % frames.Count;
        RenderPreview();
    }

    private void RenderPreview()
    {
        var previous = previewImage;
        previewImage = null;
        if (frames.Count > 0)
        {
            var frame = frames[Math.Min(previewIndex, frames.Count - 1)].Bitmap;
            previewImage = monochromeButton.Checked
                ? frame.Recolor(WindowsTheme.AppsUseLightTheme() ? Color.Black : Color.White)
                : new Bitmap(frame);
        }
        previewBox.BackColor = WindowsTheme.AppsUseLightTheme() ? Color.White : Color.FromArgb(32, 32, 32);
        previewBox.Image = previewImage;
        previous?.Dispose();
    }

    private int PreviewInterval() => (int)Math.Round((0.2 - 0.05 * speedTrackBar.Value) * 1000);

    private void UpdateAddButton() => addButton.Enabled = nameTextBox.Text.Trim().Length > 0 && frames.Count > 0;

    private void AddRunner()
    {
        try
        {
            AddedRunner = host.AddCustomRunner(nameTextBox.Text, monochromeButton.Checked, frames.Select(frame => frame.PNGData).ToList());
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (CustomRunnerException exception)
        {
            ShowError(exception.Error);
        }
    }

    private void ShowError(CustomRunnerError error) =>
        MessageBox.Show(this, CustomRunnerMessages.Message(error), Strings.CustomRunnerEditor, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private sealed class FrameImage : IDisposable
    {
        private FrameImage(Bitmap bitmap, byte[] pngData)
        {
            Bitmap = bitmap;
            PNGData = pngData;
        }

        public Bitmap Bitmap { get; }

        public byte[] PNGData { get; }

        public static FrameImage? Load(string path)
        {
            try
            {
                using var stream = new MemoryStream(File.ReadAllBytes(path));
                using var image = Image.FromStream(stream);
                if (!image.RawFormat.Equals(ImageFormat.Png) || !CustomRunnerRules.IsValidFrame(image.Width, image.Height))
                {
                    return null;
                }
                var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.DrawImage(image, 0, 0, image.Width, image.Height);
                }
                using var output = new MemoryStream();
                bitmap.Save(output, ImageFormat.Png);
                return new FrameImage(bitmap, output.ToArray());
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException or ExternalException)
            {
                return null;
            }
        }

        public void Dispose() => Bitmap.Dispose();
    }
}

internal static class CustomRunnerMessages
{
    public static string Message(CustomRunnerError error) => error switch
    {
        CustomRunnerError.NameEmpty => Strings.NameEmpty,
        CustomRunnerError.NameAlreadyExists => Strings.NameAlreadyExists,
        CustomRunnerError.NoFrames => Strings.NoFrames,
        CustomRunnerError.InvalidFrameImage => Strings.InvalidFrameImage,
        CustomRunnerError.FrameLimitExceeded => Strings.FrameLimitExceeded,
        CustomRunnerError.SavingFailed => Strings.SavingFailed,
        CustomRunnerError.RunnerInUse => Strings.RunnerInUse,
        _ => error.ToString(),
    };
}
