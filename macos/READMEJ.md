[한국어](README.md) | [English](READMEE.md) | **日本語**

# MDPlayer4Mac

Windows用のMDPlayerをmacOSで使えるように移植する個人プロジェクトである。オリジナルの
WinFormsコードはリポジトリの `MDPlayer/` にそのまま残し、macOS側のコードは `macos/` 以下の
`.NET 8` プロジェクトとして別に管理している。

最終更新: 2026-10-20

## 開発中

- MUCOM88のコンパイル済み `.mub` とソース `.muc` を再生経路に追加した。`.muc` は再生前に
  メモリ上でMUBにコンパイルし、macOS/GitのLF改行やインデントされた拡張トラック宣言もMUCOMコンパイラが
  正しく解釈するよう補正した。通常のMUBのYM2608(FM音源・SSG・リズム・ADPCM)と、拡張 `muPb` MUBの
  2×YM2608、2×YM2610、YM2151構成を、ファイルに実際に含まれるパートの分だけ初期化する。ボリュームビューと
  チャンネルビューは既存のチップレジスタ経路をそのまま使う。
- 公式の `kuma4649/mucomDotNET` ソースをGitサブモジュールとして接続した。GitHub Releaseのビルドもサブモジュールを
  一緒に取得し、ドライバDLLをアプリバンドルに含める。
- Windows版が使っていた公式 `kuma4649` ドライバのソースを追加でサブモジュールとして接続した。PMDの
  `.mml`/`.m`/`.m2`/`.mz`、MoonDriverの `.mdl`/`.mdr`、MUAP98の `.mus`/`.o`/`.ox`/`.oy` を
  再生経路に加えた。それぞれオリジナルと同じYM2608+PPZ8/PPSDRV/P86、YMF278B(またはYMF262)、
  YM2608+YM2612+CS4231構成を使う。ソース形式は一時ファイルなしでメモリ上でコンパイルし、
  PMDのPPC/PPS/PPZ/P86およびMoonDriverの同名 `.pcm` 補助データは曲ファイルと同じ
  フォルダから探す。
- MuSICAのコンパイルデータ `.bgm`、ソース `.msd`、NRTDRVの `.nrd` の再生を追加した。MuSICAは
  オリジナルのMSXドライバを実行するため、`.bgm` にはユーザーが指定した `KINROU5.DRV` が必要で、
  `.msd` をその場でコンパイルするには `KINROU4.COM` も必要である。この2つのプログラムはアプリに含めない。
  `.m3u` ファイルはプレイリストとして開き、相対パスの対応音楽ファイルを現在のプレイリストに展開して追加する。

## v0.2.3

- `msPb0102` のSCC/SCC+専用5チャンネル音符MMLに対応した。`SCCA`〜`SCCE`、インライン `@W` とSCWD波形バンクを読み、第5チャンネルの独立波形と線形音量を保持する。
- SCC再生をOPNAのFM・リズム・ADPCMおよびMSX PSGから分離した。従来のmsPb0100/0101も引き続き対応する。
- 繰り返し直後のタイ結合でジャンプ先が壊れるMUCコンパイラの不具合と、未使用ページによる誤った容量超過を修正した。
- SCC5・OPNA/PSG同時再生・波形バンク・繰り返し/ループ・不正入力の回帰テストを追加した。
  [リリースノート](release-notes/v0.2.3.md)と[MSX playback](docs/MSX-PLAYBACK.md)を参照。

## v0.2.2

- msPbのテンポ設定専用OPNAパートをオーディオミキサーから除外し、同じPSG/SCC演奏の
  MUCとMUBで再生音量が異なる問題を修正した。実際のOPNA演奏とSCC-SSGフォールバックは維持する。
- ループ開始点と未完成コマンドのチップ判定を補完し、テンポ・休符だけの無音曲も再生されるようにした。
- OPNAリズムの初期化と、複数ドラムの自動パン待機コマンドを修正した。
- 最新MUCOMSXのデフォルトテンポ、SCC波形転送・キャッシュ、rawコマンドの解読、MUCのBOM/EOF処理を反映した。
- MUB/MUCそれぞれ20曲の再生・終了テストと、チップ判定・リロードの回帰テストに合格した。
  [変更内容](release-notes/v0.2.2.md)と[MSX playback](docs/MSX-PLAYBACK.md)を参照のこと。

## v0.2.1

- MUCOMSX raw v2のPSG、SCC/SCC+およびソースOPNA SSGのレジスタ転送に対応した。
  宣言されたチップに合わせてエミュレーターを初期化し、SCC+の5番チャンネルの独立波形を処理する。
- MSX PSGのエミュレータークロックとSSGクロック変換を補正した。
- rawストリームはMSXプレイヤーと同じ整数サンプルタイマーを使い、テンポ変更時や
  タイマー応答時の累積再生時間の誤差を減らした。通常のMUCOMタイマーは維持する。
- 合成MUB/MUCそれぞれ15曲と、Z80レジスタ・タイマー比較を含む回帰テストを追加した。
  詳細は[MSX playback](docs/MSX-PLAYBACK.md)を参照のこと。

## v0.2.0

- MUCOMSXの `msPb0100`/`msPb0101` MUBと、ネイティブ `MSX` パートを持つMUCに対応した。
  MSX-MUSIC、内蔵PSG、SCC+ SSGの再生と、拡張波形・ゲインコマンドを処理する。
- MUCOMコンパイラ・ドライバの修正版を別プロジェクトとして管理し、OPNA FM3モードの保持、
  Kパートのレジスタルーティング修正、ADPCMにのみ適用される `#PCMInvert` の修正を含む。
- OPNAリズムWAVをアプリに同梱し、MSX再生の回帰テストとZ80ドライバのレジスタ
  比較データを追加した。
- 前回のリリース以降に追加されたFMP、NDPおよびWAV/AIFF/MP3/M4A/AAC/FLAC/OGGの再生と、
  外部ドライバのパス設定、Xcodeビルド対応を含む。

## v0.1.7

- MSX-AUDIO(Y8950)ADPCMのチャンネルビューが、レジスタの固定Total Levelしか表示していなかった問題を修正した。
  Delta-T ADPCMの実際のデコード信号を使うようになったため、再生中にゲージが動き、ボリュームビューの
  ユーザーゲインに関係なく元の信号を基準に表示される。

## v0.1.6

- VGMのK051649/K052539フラグを読み取り、通常のSCCは `SCC`、SCC+は `SCC+` と表示するようにした。
  SCC+の5番チャンネルの独立ウェーブRAMへの書き込みもそのまま処理する。
- 曲情報画面で、タイトル、作曲者、Notesなどの長い文字列を `...` で省略しないようにした。ウィンドウを
  広げると情報列も一緒に広がり、全文を読めるようになる。
- ダッシュボードにファイルをドロップしてプレイリストを入れ替える際、同じチップ構成の曲であればウィンドウサイズを
  そのまま維持するようにした。実際のチャンネルパネル構成が変わる場合にのみサイズを合わせ直す。

## 以前の移植作業

### v0.1.5

- Windowsオリジナルの `frmSetting` の構成を基準に、Avaloniaの設定ウィンドウを追加した。`Output`、
  `Sound`、チップ別エミュレーション、MIDI、プレイリスト、ネットワーク、その他、Aboutを含むオリジナルのタブを
  確認できる。現在macOSで実際に接続している項目はOutputで、Windows専用の機能は
  誤動作しないよう無効状態で表示する。
- Outputでは、Core Audioのシステムデフォルトデバイス、レンダリングバッファの遅延時間(25〜500 ms)、再生前の
  待機時間、サンプルレートを設定する。保存した出力設定は、次の再生から新しいAudio Queueで
  適用される。
- 設定ウィンドウでは長いセクション見出し・オプションを折り返し、セクションボックスがタブ内容の幅を超えないよう固定した。
  縦スクロールバーはタブの右端に配置する。
- WindowsオリジナルのSettingスプライトとAboutのイラストを取り込んだ。設定アイコンはダッシュボード右
  下の16×16ユーティリティボタンとして分離し、タイムラインの幅を占有しないようにした。
- ダッシュボードのキーボードショートカットを追加した。`Q W E R T Y U` はStop/Pause/Previous/Slow/
  Play/Fast/Next、`A S D F G H J` はOpen/Playlist/Information/Volume/Channel/Zoom/
  Loopの順である。無効なボタンはキーボードからも実行せず、Cmd/Ctrl+A、Delete、
  Backspaceなどのプレイリスト編集キーは維持する。

### v0.1.4

- PCM・ADPCM系を含むチャンネルビューのレベルメーター経路を全数点検した。再生前とStop後の
  メーター初期値は0にリセットし、YM2610/YM2608などのADPCM-A・ADPCM-Bメーターは実際のチャンネルの
  動作に応じて変化するよう補正した。
- ADPCMメーターは、ボリュームビューでユーザーが設定したゲインとは独立して、元のチャンネル信号を表示する。
  小さなADPCM-Bの変化も確認しやすいよう、表示感度を引き上げた。
- チャンネルビューのサイズは25%・50%・75%・100%に対応し、デフォルトの75%から
  `75 → 100 → 75 → 50 → 25 → 50 → 75` の順で往復する。ダッシュボードの時間表示と進行バーも
  これに合わせて縮小・拡大される。
- 進行バーの下には、現在の曲で使用するチップを常に表示する。同じチップが複数インスタンス
  使われている場合は、`2xYM2610`、`3xDCSG` のようにまとめて表示する。

### v0.1.3

- Windowsオリジナルのピクセルスプライトを使い、再生・停止・一時停止・前/次・巻き戻し/早送り、
  リピート/ランダム、チャンネル・ボリューム・プレイリスト・曲情報・50%縮小ボタンをプレイヤーのダッシュボードに配置した。
- ダッシュボードに、Windowsオリジナルのフォントを元にした現在時間/全体の長さ/ループ位置の表示と進行バーを
  追加した。ファイル名領域の右側には `-60 dB`〜`+20 dB` の範囲のマスターボリュームフェーダーを置いた。
- 曲情報ビューはGD3メタデータと歌詞イベントを表示する。チャンネルビューはVGMのチップパネルを
  FM → SSG/PSG → PCMの順に並べ、Yamaha複合チップのサブ出力もこの順序に合わせる。
- ボリュームビューは実際に使用されたチップインスタンスをそれぞれ表示し、FM/SSG/ADPCM/PCMのように分離可能な
  出力は独立したスライダーで制御する。YM2612のFM/ADPCM、Y8950のFM/ADPCMも含まれる。
- MDXはOPM(YM2151)とPCM8/PDX構成を別々に表示し、PDXが見つからなくてもOPMの再生を
  試み続ける。MDXドライバのループは、VGMと同じ最大2回を基準に終了処理する。
- VGM 1.01以前の単一FMクロックヘッダーは、実際のコマンドストリームを解析してYM2413/YM2612/YM2151のうち
  正しいチップ1つだけを初期化する。これにより、YM2413の曲にYM2612・YM2151のパネルが一緒に表示されていた
  問題を防ぎ、以降のバージョン専用のヘッダーフィールドもバージョン条件で安全に読み取る。
- Finderからディレクトリをドロップすると、対応拡張子の音楽ファイルをサブフォルダまで安定した名前順で
  探してプレイリストに追加する。プレイリスト領域へのドロップは追加、ダッシュボードへのドロップは現在のリストの置き換えである。
- macOS上部のアプリメニューとDockの名前を `MDPlayer4Mac` に統一した。`dotnet run` のように `.app`
  バンドルの外で実行する場合も、AppKitブリッジが同じ名前とWindowsオリジナルのメインアイコンを
  設定し、GitHub Releaseの `.app` バンドルは `MDPlayer4Mac.icns` を含む。

## 現在できること

- Core Audio Audio Queueを使ったリアルタイム再生
- Windows版のスプライトを使った再生ボタンとチップチャンネル表示
- チャンネルビュー、ボリュームビュー、プレイリスト専用ビュー、曲情報ビュー
- チップ別/出力ブロック別のボリューム調整とマスターボリューム
- 複数ファイルのプレイリスト、ファイルおよびフォルダのドラッグ&ドロップ、複数選択と削除
- 次の曲の自動再生、リピート/ランダム再生、再生速度の調整、自動再生
- チャンネルビューのサイズ切り替え: 25% / 50% / 75% / 100%(デフォルト75%)
- Windows版の設定ウィンドウ構成とAbout画面。現在実際に接続されている設定はOutputが中心である。
- タグをプッシュすると、Apple Silicon用のmacOSアプリを自動でビルドしてGitHub Releaseにアップロードする。

## 対応フォーマット

UIで開いたりドロップしたりできる拡張子は以下のとおり。

```text
.vgm  .vgz  .xgm  .xgz  .sid  .mnd  .zms  .zmd
.mdx  .mdr  .mdl  .mub  .muc  .mml  .m  .m2  .mz
.mus  .o  .ox  .oy  .mgs  .msd  .bgm  .nrd  .ndp  .mid  .rcp  .rcs
.opi  .ovi  .ozi  .mpi  .mvi  .mzi
.nsf  .gbs  .hes  .s98  .ay  .zgm
.wav  .aif  .aiff  .mp3  .m4a  .aac  .flac  .ogg
.m3u (プレイリスト)
```

- `.vgz`、`.xgz` はそれぞれgzip圧縮のVGM/XGMとして処理する。
- 古いVGM 1.01以前のファイルは、コマンドストリームを確認してYM2413/YM2612/YM2151のうち実際に使用するチップだけを
  初期化する。
- MDXはX68000系のフォーマットである。同じフォルダのPDXが見つかればPCM8/ADPCM再生に使い、
  PDXがなくてもOPM(YM2151)パートの再生は試み続ける。
- `.mdr`/`.mdl` はMoonDriverのOPL4/OPL3フォーマットである。同名の `.pcm` ファイルがあれば一緒に
  使う。`.mml` はPMDのソースで、`.m`/`.m2`/`.mz` はPMDのコンパイルデータである。`.mus` はMUAP98の
  ソースで、`.o`/`.ox`/`.oy` はMUAP98のコンパイルデータである。
- `.mgs` はMGSDRV形式である。MGSDRVは無改変での再配布を許可しているため(`licenses/MGSDRV/`)、
  `macos/MDPlayerUI/Assets/Drivers/` に `MGSDRV.COM` を置くとアプリバンドルに同梱される。バンドルに
  ない場合は、ユーザーのDriversフォルダか、Setting → Other → MGSDRVで指定したファイルを使う。
- オリジナルのドライバやROMが必要なフォーマット(MuSICA、NDP、FMP、ZMUSIC、OPL4の `yrw801.rom`)は、
  `~/Library/Application Support/MDPlayer4Mac/Drivers/` にファイルを置けばよい。Windows版はexeの
  隣を探すが、macOSではその場所が署名済みアプリバンドルの中になるため、別にフォルダを設けている。設定ウィンドウのOther
  タブのDriver filesからフォルダを開き、各ファイルが見つかったかどうかを確認できる。
- `.bgm` はMuSICAのコンパイルデータで、Setting → Otherで `KINROU5.DRV` を指定する必要がある。
  `.msd` はMuSICAのソースなので、同名の `.vcd` があれば一緒に読み込み、その場でコンパイルするために
  `KINROU4.COM` も指定する必要がある。どちらのファイルもライセンス上、アプリに同梱したり再配布したりしない。
- `.ndp` はMSX PSGドライバNDPの形式である。オリジナルの `NDP.BIN` をユーザーのDriversフォルダに置く必要があり、
  MGSDRVと同じZ80/MSXホストで実行してAY8910に出力する。
- `.opi`/`.ovi`/`.ozi` はPC-98 FMPのコンパイルデータで、`.mpi`/`.mvi`/`.mzi` はFMPのソースである。
  オリジナルの `FMP.COM` をPC-98(80286)エミュレーターNise98で実行し、YM2608+PPZ8に出力する。ソース
  形式は再生前に `FMC.EXE` でメモリ上でコンパイルする。どちらのファイルもユーザーのDriversフォルダに置く必要があり、
  PPZ8はNise98が直接エミュレートするため `PPZ8.COM` は不要である。PVI/PZIのPCMファイルは
  曲ファイルと同じフォルダから探す。
- `.nrd` はNRTDRV形式で、ファイルが使用するYM2151(1/2個)・AY8910の組み合わせを自動で構成する。
- `.mid` はStandard MIDI File、`.rcp` はRCPシーケンス、`.rcs` はRCP+PCM8シーケンス形式である。
  macOS内蔵のDLS General MIDIシンセサイザーで出力する。RCP/RCSが参照する `.cm6`/`.gsd` および
  companion `.rcp` は同じフォルダから自動で探す。外部MIDIデバイスとVSTへのルーティングはまだない。
- `.wav`/`.aif`/`.aiff`/`.mp3`/`.m4a`/`.aac`/`.flac` はmacOSのAudioToolbox(ExtAudioFile)で
  デコードし、出力サンプルレートの変換もシステムが処理する。`.ogg` はmacOSがデコードしないため、
  NVorbisで読み込む。Windows版の `.wma` はmacOSでは対応しない。macOS以外の環境で動かす
  EngineSmokeTestはWAVとOGGのみを読み込む。`macos/tools/audio-file-smoke.sh` で生成したテストトーンを
  各形式でデコードし、長さと音程を検査できる(CIのaudio-filesジョブ)。
- `.m3u` はオーディオファイルではなく、プレイリストファイルである。`#EXTINF` のタイトルと相対/絶対ローカルパスを
  読み込み、ネットワークURLは現在対応していないためスキップする。
- `.mub` はMUCOM88のコンパイル済みデータフォーマットで、`.muc` は再生時にメモリ上で同じ形式に
  コンパイルする。`#voice`、`#pcm`、`#@pcm` などで外部データを指定したMUCは、該当ファイルをMUCと同じ
  フォルダに置く必要がある。
- MUCOM88のポルタメントは長さを最初の音の後に書く `{c4>c}` 形式である。MUCEditマニュアルのように `}` の後に
  書いた `{c>c}4` も、コンパイル直前にメモリ上で `{c4>c}` に移して同じ結果で再生する。MUCファイル自体は
  変更せず、最初の音にすでに長さがある場合はそのままにする。
- 対応リストにある形式でも、すべてのドライバとチップの組み合わせが完成しているわけではない。ロードできない
  組み合わせはステータスバーにエラーとして表示される。

## プレイヤーの使い方

ファイルを最初に開くとチャンネルビューが表示される。チャンネルビューとボリュームビューの下には約3.5行分の高さのプレイリストが
付き、プレイリストボタンを押すと20行分の高さのリスト専用画面に切り替わる。20曲を超える場合は
リスト内でスクロールする。

- プレイリスト領域にファイルやフォルダを置くと、現在のリストの後ろに追加される。
- 上部のダッシュボードに置くと、現在のプレイリストを新しいリストに置き換える。
- フォルダをドロップすると、サブフォルダまで探して対応ファイルを名前順に追加する。
- `.m3u` を開くかドロップすると、リスト内の対応音楽ファイルを展開して追加する。
- Cmd+A、Shift/Cmd選択、Delete/Backspace、Escapeで一般的なリスト編集ができる。
- Stopを押しても現在のチャンネルビューは閉じない。
- 曲が終わると次の曲を再生する。リピートボタンは現在の曲のリピートで、もう一度押すとランダム再生に
  切り替わる。
- Playを短く押すと、再生速度を1xに戻してから再生または再開する。Playを2秒以上押すか
  Force Clickすると、自動再生のオン/オフを切り替える。

### キーボードショートカット

上段のボタン列は `Q W E R T Y U`、下段のボタン列は `A S D F G H J` に左から対応する。

```text
Q Stop       W Pause      E Previous   R Slow
T Play       Y Fast       U Next
A Open       S Playlist   D Information  F Volume
G Channel    H Zoom       J Loop / Random
```

## チャンネルビューとボリュームビュー

チャンネルビューは、VGMで実際に使用するチップをFM → SSG/PSG → PCMの順に表示する。同じチップが
複数ある場合はすべて表示し、ダッシュボードには `2xYM2610` のようにまとめて表示する。Yamaha複合チップは
FM、SSG、ADPCM、PCMなど性質の異なる出力を分けて表示する。

ボリュームビューは、現在の曲で使用するチップインスタンスのみを表示する。FM/SSG/ADPCM/PCMのように独立した
出力は、可能な範囲でそれぞれ調整できる。Reset Volumeはファイル内に保存されたデフォルトボリュームに
戻し、デフォルト値がないフォーマットでは0 dBを使う。

## 設定

ダッシュボード右下のSettingアイコンから開ける。

- `Output` では、Core Audioのデフォルト出力、バッファ遅延時間(25〜500 ms)、再生前の待機時間、
  サンプルレートを設定する。変更値は次の再生から適用される。
- Sound、チップ別エミュレーション、MIDI、プレイリスト、ネットワーク、その他、AboutはWindows版の構成と
  項目名をできる限り維持した。
- SCCI/C86CTL、ASIO/WASAPI、VST、リアルタイムMIDI I/OのようにmacOSにまだない機能は無効状態で
  表示される。実装されていない設定が再生に影響しないようにするためである。

## 開発環境

- macOS
- Xcode Command Line Tools(`xcrun clang` を含む)
- .NET SDK 9.0.300以上(10推奨)。Avalonia 12のXAMLソースジェネレーターがRoslyn 4.14を要求するため、
  SDK 8ではビルドできない(ターゲットフレームワークはそのまま `net8.0`)
- 初回のNuGet復元時にインターネット接続

`MDPlayerUI` はAvaloniaを使っている。Force ClickはAvaloniaの通常のポインターイベントとして届かない
ため、`MDPlayerUI/Native/ForceTouchMonitor.m` を小さなApple Silicon用dylibとしてビルドして
併用する。このライブラリは、開発時の実行でAppKitのアプリ名とDockアイコンを合わせる役割も
担う。

## ビルドと実行

リポジトリのルートで実行すればよい。

```bash
dotnet build macos/MDPlayerUI/MDPlayerUI.csproj -c Release
dotnet run --project macos/MDPlayerUI/MDPlayerUI.csproj -c Release
```

GUIなしで再生エンジンだけを確認するには、以下のように実行すればよい。

```bash
dotnet run --project macos/LivePlayer/LivePlayer.csproj -c Release -- /path/to/song.vgm
```

VGMをWAVにレンダリングするスモークテストは以下のとおり。

```bash
dotnet run --project macos/EngineSmokeTest/EngineSmokeTest.csproj -c Release -- /path/to/song.vgm /path/to/output.wav
```

### Xcodeでのビルド

`macos/MDPlayer4Mac.xcodeproj` を開き、`MDPlayer4Mac` スキームを実行(⌘R)すればよい。アプリ本体は
.NET/Avaloniaのコードなので、Xcodeが直接コンパイルするわけではない。アプリターゲットの `Build .NET App` ビルドフェーズが
`macos/Xcode/build-app.sh` を呼び出して `dotnet publish`(自己完結、単一ファイル、`osx-arm64`)を
実行し、成果物を `MDPlayer4Mac.app/Contents/MacOS` に配置する。`Info.plist` とアイコン、署名は
Xcodeが処理する。バンドル構成はリリースワークフローと同じである。

- 事前準備: .NET SDK 9.0.300以上(10推奨)、サブモジュール(`git submodule update --init --recursive`)
- XcodeのDebug/Release構成が、そのまま `dotnet publish -c Debug/Release` に渡される
- `dotnet` は `PATH`、`/usr/local/share/dotnet`、`/opt/homebrew/bin`、`~/.dotnet` の順に
  探す。別の場所にある場合は、スキームの環境変数に `DOTNET=/path/to/dotnet` を指定すればよい
- 署名はデフォルトで "Sign to Run Locally"(ad-hoc)である。Developer IDで署名するにはターゲットの
  Signing設定を変えるだけでよく、バンドル内のdylibも同じ証明書で署名される
- LLDBは.NETのマネージドコードをデバッグできないため、スキームはデバッガーなしでアプリを実行する。デバッグが
  必要な場合は、VS CodeやRiderの.NETデバッガーを実行中のプロセスにアタッチすればよい
- アプリのバージョンはターゲットのGeneralタブ(`MARKETING_VERSION`)で変更する
- `macos/` 以下を変更してブランチにプッシュすると、`.github/workflows/xcode-build.yml` がmacOSランナー上で
  Debug/Releaseを `xcodebuild` でビルドし、バンドル構成・署名の検証と15秒間の起動確認まで行う

## リリース

`v*` タグをプッシュすると、`.github/workflows/release-macos.yml` がApple Silicon用の自己完結アプリを
ビルドしてReleaseに添付する。

```bash
git tag v0.2.3
git push origin v0.2.3
```

生成物は `MDPlayer4Mac-osx-arm64.zip` である。展開すると1つの `MDPlayer4Mac.app` バンドルが
得られる。.NETランタイム、Avalonia、Force Touch dylib、アプリアイコンはバンドル内に含まれる。

## Gatekeeperについて

現在の配布物はCIでad-hoc署名のみを適用している。Apple Developer ID署名とnotarizationはまだ
行っていないため、別のMacで初めて実行するときに、Gatekeeperが開発元を確認できないという
警告を表示することがある。これは現在の配布方式では正常な動作である。

アプリは必ずこのリポジトリの[GitHub Release](https://github.com/ToughkidDev/MDPlayer4Mac/releases)
から入手した `MDPlayer4Mac-osx-arm64.zip` のみを使うこと。入手元が確かでないアプリに以下の
方法を適用してはならない。

最も推奨する実行方法は以下のとおり。

1. ZIPを展開し、`MDPlayer4Mac.app` を `アプリケーション` フォルダに移動する。
2. アプリを一度開き、Gatekeeperの警告を表示させる。
3. **システム設定 → プライバシーとセキュリティ**を開き、画面下部の**このまま開く**を
   押す。
4. 再び表示された確認ダイアログで**開く**を押し、macOSのログインパスワードを入力する。

この許可はそのアプリにのみ保存されるため、以降は通常のアプリと同じようにダブルクリックで実行できる。
`このまま開く` ボタンは、初回の起動がブロックされてから約1時間表示される。

システム設定にボタンが表示されない場合に限り、GitHub Releaseから入手したファイルであることを確認したうえで、
ターミナルで隔離属性を削除できる。

```bash
xattr -dr com.apple.quarantine "/Applications/MDPlayer4Mac.app"
```

このコマンドはアプリ1つにのみ適用され、Gatekeeper自体を無効にするものではない。ただし、ダウンロードファイルのセキュリティ
表示を直接取り除く方法であるため、入手元を確認できないアプリや、`壊れている`・マルウェアの警告が
出るアプリには使ってはならない。その場合はアプリを削除し、ReleaseのZIPを再度ダウンロードすること。

Appleの最新の案内でも、一度起動を試みた後に**プライバシーとセキュリティ → このまま開く**でそのアプリだけを
許可する方法を推奨している。[Apple Gatekeeperの案内](https://support.apple.com/guide/mac-help/open-a-mac-app-from-an-unknown-developer-mh40616/mac)

## 既知の範囲と今後の作業

- Apple Developer ID署名とnotarization
- SCCI/C86CTLによる実チップ出力、Windows専用のASIO/WASAPI出力
- VSTホスティングとリアルタイムMIDIパススルー
- LHA圧縮ファイルの処理
- フォーマットとチップの組み合わせごとの実曲回帰テストの拡充

`Z80dotNet 1.0.6` は.NET Framework向けのパッケージのため、`NU1701` 警告が残る。現在のビルドと
再生は可能だが、将来的にはnet8.0/netstandard互換のパッケージに置き換えるのが望ましい。

## MUCOMSX MSX拡張再生

`msPb0100` と `msPb0101` を個別に判別し、MSX-MUSIC、内蔵PSG、SCC+ SSGを再生する。
ネイティブ `MSX` パートを含むMUCのメモリ上でのコンパイルにも対応する。`@I`、`@W`、`pe`、
`#PCMInvert` の動作と近似の限界、回帰テストは[MSX再生ガイド](docs/MSX-PLAYBACK.md)にまとめた。

## ライセンス

オリジナルのMDPlayerと、`MDSound/` 内に含まれる外部コードのライセンスに従う。新しいコードを追加したり
配布したりする際は、オリジナルファイルの著作権とGPL系の条件をあわせて確認する必要がある。

MUBの再生には[mucomDotNET](https://github.com/kuma4649/mucomDotNET)を、PMDの再生には
[PMDDotNET](https://github.com/kuma4649/PMDDotNET)をGitサブモジュールとして含み、両プロジェクトの
GPL-3.0ライセンスと著作権表示にあわせて従う。MoonDriverとMUAPの再生には、それぞれ
[MoonDriverDotNET](https://github.com/kuma4649/MoonDriverDotNET)、
[muapDotNET](https://github.com/kuma4649/muapDotNET)のソースをGitサブモジュールとして含む。この2つの
リポジトリはGitHub上に別途ライセンスファイルが表示されていないため、配布前に原作者とリポジトリの
著作権・配布条件を必ず再確認する必要がある。
