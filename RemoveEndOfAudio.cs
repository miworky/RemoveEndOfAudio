using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using ScriptPortal.Vegas;
using System.Globalization;
using System.Drawing;

namespace RemoveEndOfAudio
{
    public class EntryPoint
    {
        public void FromVegas(Vegas vegas)
        {
            if (vegas.Project.Tracks.Count == 0)
            {
                // トラックがないときは何もしない
                MessageBox.Show("Error: No tracks");
                return;
            }

            // パラメータを設定するダイアログを開く
            // 以下をユーザーに選択させる：
            //  オーディオファイルのあるトラック番号
            //  オーディオファイルの最後を削除する時間(ms)
            Tuple<DialogResult, string, string> dialogResult = DoDialog(vegas);
            DialogResult result = dialogResult.Item1;
            if (result != DialogResult.OK)
            {
                return;
            }

            string MusicTrackNo1BaseString = dialogResult.Item2;
            string TimeMsToRemoveString = dialogResult.Item3;

            Tuple<bool, int> tupleMusicTrack = to0base(MusicTrackNo1BaseString);
            if (!tupleMusicTrack.Item1) {
               return;
            }
            int MusicTrackNo0Base = tupleMusicTrack.Item2;

            if (vegas.Project.Tracks.Count <= MusicTrackNo0Base)
            {
                MessageBox.Show("Error: Requires more tracks.");
                return;
            }

            int TimeMsToRemove = 0;
            if (!int.TryParse(TimeMsToRemoveString, out TimeMsToRemove))
            {
                MessageBox.Show("Cannot parse VideoTrackNoString");
                return;
            }
            if (TimeMsToRemove == 0)
            {
                MessageBox.Show("TimeMsToRemoveString must not be 0.");
                return;
            }

            Track musicTrack = vegas.Project.Tracks[MusicTrackNo0Base];
            if (!musicTrack.IsAudio()) {
                MessageBox.Show("Error: musicTrack you designated is NOT Audio track.");
                return;
            }

//            string message = VideoTrackNo0Base.ToString() + " " + MusicTrackNo0Base.ToString() + " " + TimeMsToRemove.ToString();
//            MessageBox.Show(message);


            // ダイアログを開き出力するログのファイルパスをユーザーに選択させる
            string saveFilePath = GetFilePath(vegas.Project.FilePath, "RemoveEndOfAudio");
            if (saveFilePath.Length == 0)
            {
                return;
            }

            System.IO.StreamWriter writer = new System.IO.StreamWriter(saveFilePath, false, Encoding.GetEncoding("Shift_JIS"));

            // オーディオファイルの最後を指定時間削除する
            bool err = RemoveEndOfAudioInternal(musicTrack, TimeMsToRemove, writer);
            if (!err) {
                MessageBox.Show("Error: RemoveEndOfAudioInternal");
                return;
            }

            writer.Close();
            MessageBox.Show("終了しました。");
        }

        // オーディオファイルの最後を指定時間削除する
        // musicTrack: 削除するメディアファイルのオーディオトラック
        // TimeMsToRemove: 削除する時間(ms)
        // writer: ログ出力用stream
        private bool RemoveEndOfAudioInternal(Track musicTrack, int TimeMsToRemove, System.IO.StreamWriter writer)
        {
            // music Track の全イベントについて処理する
            foreach (TrackEvent trackEvent in musicTrack.Events)
            {
              Take take = trackEvent.ActiveTake;
              {

                  Media media = take.Media;
                  string path = media.FilePath; // メディアのファイルパス
                  if (!IsVideo(path))
                  {
                      // 動画でないならば無視
                      continue;
                  }
                  
                  // オーディオファイルの後ろを指定時間削除

                  // Eventを分割する時刻を生成
                  Timecode splitOffset = trackEvent.Length - Timecode.FromMilliseconds(TimeMsToRemove);

                  // 指定時刻でEventを分割
                  TrackEvent splittedEvent = trackEvent.Split(splitOffset);

                  // 分割した後ろのEventがmusicTrackに追加されているので、削除
                  musicTrack.Events.Remove(splittedEvent);
                  
                  // ログ出力
                  writer.WriteLine(path);
               }
            }
            
            return true;
        }

        // ダイアログを開きファイルパスをユーザーに選択させる
        private string GetFilePath(string rootFilePath, string preFix)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = preFix + System.IO.Path.GetFileNameWithoutExtension(rootFilePath) + ".txt";
            sfd.InitialDirectory = System.IO.Path.GetDirectoryName(rootFilePath) + "\\";
            sfd.Filter = "テキストファイル(*.txt)|*.txt";
            if (sfd.ShowDialog() != DialogResult.OK)
            {
                return "";
            }

            return sfd.FileName;
        }

        private Tuple<bool, int> to0base(string string1base)
        {
            int int1base = 1;
            if (!int.TryParse(string1base, out int1base))
            {
                MessageBox.Show("Cannot parse " + string1base);
                return Tuple.Create(false, int1base);
            }
            if (int1base == 0)
            {
                MessageBox.Show("Number must not be 0.");
                return Tuple.Create(false, int1base);
            }
            int int0base = int1base - 1;

            return Tuple.Create(true, int0base);
        }

        // 与えられたファイルパスの拡張子が、extentionsに含まれている拡張子であるかを調べる
        private bool isSameExtention(string path, string[] extentions)
        {
            // ファイルパスから拡張子を取得
            string searchExtention = Path.GetExtension(path).ToLower();

            // extentionsに含まれている拡張子かを調べる
            foreach (string extention in extentions)
            {
                if (extention == searchExtention)
                {
                    return true;
                }
            }

            return false;
        }

        // 画像のファイルパスかどうかを返す
        // 画像かどうかは、拡張子で判断する
        // HEIFの exif は読み方がわからないので、非対応
        private bool IsImage(string path)
        {
            // 画像の拡張子リスト
            string[] Extentions = new string[] { ".jpg", ".jpeg"};

            return isSameExtention(path, Extentions);
        }

        // 動画のファイルパスかどうかを返す
        // 動画かどうかは、拡張子で判断する
        private bool IsVideo(string path)
        {
            // 動画の拡張子リスト
            string[] Extentions = new string[] { ".mp4", ".mov" };

            return isSameExtention(path, Extentions);
        }

       // --------------- dialog
       private Tuple<Form, TableLayoutPanel> CreateForm(string title)
       {
            Form form = new Form();
            {
               form.SuspendLayout();
               form.AutoScaleMode = AutoScaleMode.Font;
               form.AutoScaleDimensions = new SizeF(6F, 13F);
               form.FormBorderStyle = FormBorderStyle.FixedDialog;
               form.StartPosition = FormStartPosition.CenterParent;
               form.MaximizeBox = false;
               form.MinimizeBox = false;
               form.HelpButton = false;
               form.ShowInTaskbar = false;
               form.Text = title;
               form.AutoSize = true;
               form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            }

            TableLayoutPanel layout = new TableLayoutPanel();
            {
               layout.AutoSize = true;
               layout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
               layout.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
               layout.ColumnCount = 3;
               layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
               layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
               layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            }
            form.Controls.Add(layout);

            return Tuple.Create(form, layout);
        }

        private void CreateOKCancelButton(Form form, TableLayoutPanel layout)
        {
            FlowLayoutPanel buttonPanel = new FlowLayoutPanel();
            buttonPanel.FlowDirection = FlowDirection.RightToLeft;
            buttonPanel.Size = Size.Empty;
            buttonPanel.AutoSize = true;
            buttonPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttonPanel.Margin = new Padding(8, 8, 8, 8);
            buttonPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            layout.Controls.Add(buttonPanel);
            layout.SetColumnSpan(buttonPanel, 3);

            {
                Button cancelButton = new Button();
                cancelButton.Text = "Cancel";
                cancelButton.FlatStyle = FlatStyle.System;
                cancelButton.DialogResult = DialogResult.Cancel;
                buttonPanel.Controls.Add(cancelButton);
                form.CancelButton = cancelButton;
            }

            {
                Button okButton = new Button();

                okButton.Text = "OK";
                okButton.FlatStyle = FlatStyle.System;
                okButton.DialogResult = DialogResult.OK;
                buttonPanel.Controls.Add(okButton);
                form.AcceptButton = okButton;
            }
        }

        private Label AddLabel(TableLayoutPanel layout, string text)
        {
           Label label;

           label = new Label();
           label.Text = text;
           label.AutoSize = false;
           label.TextAlign = ContentAlignment.MiddleLeft;
           label.Margin = new Padding(8, 8, 8, 4);
           label.Anchor = AnchorStyles.Left | AnchorStyles.Right;
           layout.Controls.Add(label);
           layout.SetColumnSpan(label, 3);

           return label;
        }

        private TextBox AddTextBox(TableLayoutPanel layout, string text)
        {
            TextBox textBox = new TextBox();
            {
                textBox.Text = text;

                textBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                textBox.Margin = new Padding(16, 8, 8, 4);
                layout.Controls.Add(textBox);
                layout.SetColumnSpan(textBox, 2);
            }

            return textBox;
        }

        private Tuple<DialogResult, string, string> DoDialog(Vegas vegas)
        {
            Tuple<Form, TableLayoutPanel> tuple = CreateForm("RemoveEndOfAudio");
            Form form = tuple.Item1;
            TableLayoutPanel layout = tuple.Item2;

            // ----------- Music Track No
            AddLabel(layout, "Music Track No(1-base):");
            TextBox MusicTrackNoBox = AddTextBox(layout, "4");

            // ----------- TimeMSToRemove
            AddLabel(layout, "Time to remove(ms):");
            TextBox TimeMSToRemoveBox = AddTextBox(layout, "50");

            // ----------- Cancel / OK
            CreateOKCancelButton(form, layout);

            form.ResumeLayout();

            string textMusicTrackNo = "";
            string textTimeMSToRemove = "";
            DialogResult result = form.ShowDialog(vegas.MainWindow);
            if (DialogResult.OK == result)
            {
                textMusicTrackNo = MusicTrackNoBox.Text;
                textTimeMSToRemove = TimeMSToRemoveBox.Text;
            }

            return Tuple.Create(result, textMusicTrackNo, textTimeMSToRemove);
        }

    }

}
