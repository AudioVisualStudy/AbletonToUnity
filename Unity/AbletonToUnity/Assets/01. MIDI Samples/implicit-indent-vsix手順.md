# implicit-indent を .vsix で Cursor にインストールする手順

## 1. .vsix をダウンロードする

ブラウザのアドレスバーに次の URL を**そのまま貼り付けて** Enter を押してください。  
（implicit-indent のバージョン 1.1.1 がダウンロードされます）

```
https://marketplace.visualstudio.com/_apis/public/gallery/publishers/jemc/vsextensions/vscode-implicit-indent/1.1.1/vspackage
```

- ダウンロードが始まり、`vspackage` または `vscode-implicit-indent-1.1.1.vsix` のようなファイルが保存されます。
- 保存場所はブラウザの「ダウンロード」フォルダが一般的です。
- ファイル名が `vspackage` の場合は、拡張子を `.vsix` に変更してください（右クリック → 名前の変更 → 末尾を `.vsix` に）。

---

## 2. Cursor で「VSIX からインストール」する

1. Cursor を開く。
2. **Ctrl+Shift+P** で**コマンドパレット**を開く。
3. **「vsix」** や **「Install from VSIX」** と入力する。
4. 一覧から **「Extensions: Install from VSIX...」** を選んで Enter。
5. 開いたダイアログで、さきほどダウンロードした **.vsix ファイル**（または `vspackage` を `.vsix` にリネームしたもの）を選んで **開く**。
6. インストールが終わったら、必要に応じて Cursor を**再読み込み**（ウィンドウの再起動や「Reload」）する。

これで、空行にカーソルを移動したときにインデント位置に合わせる implicit-indent が使えるようになります。
