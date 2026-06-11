Y-OSC-NoteSender（再配布用・静的ビルド）
=========================================

このフォルダの内容を、そのまま Ableton User Library にコピーしてください。

【コピー先】
  Windows:
    Documents\Ableton\User Library\Presets\MIDI Effects\Max MIDI Effect\
  macOS:
    ~/Music/Ableton/User Library/Presets/MIDI Effects/Max MIDI Effect/

【同梱ファイル】
  Y-OSC-NoteSender.amxd   … Max MIDI Effect デバイス
  y_osc_note_sender.js    … 上記 amxd と同じフォルダに置く（必須）

【使い方】
  1. 上記フォルダへ 2 ファイルをコピー
  2. Live を再起動（またはブラウザを更新）
  3. User Library → MIDI Effects → Max MIDI Effect → Y-OSC-NoteSender
     を各 MIDI トラックの MIDI Effect スロットへ配置
  4. Unity 側 OscHub が UDP 11001 で待受していることを確認

【送信仕様】
  アドレス: /y-osc/note
  引数: iiii（TrackIndex, SlotIndex, Pitch, Velocity）
  送信先: 127.0.0.1:11001

Python / Ableton MCP / AgentM4L は不要です。

【開発者向け】
  amxd の再生成: 親フォルダで python build_static_dist.py
