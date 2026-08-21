# CH32X033 0.96 吋 USB 螢幕：設計分析與 Repository 提案

> 2026-08-21 修訂：接線圖確認實際 MCU 為 CH32X033F8P6（CH32X035 系列），不是最初文字規格的 CH32V003F4P6。下方早期限制分析保留作決策紀錄；目前實作基準以 `docs/hardware.md` 為準。

## 1. 結論摘要

- CH32V003F4P6 只有 **16 KiB Flash、2 KiB SRAM、最高 48 MHz**，沒有原生 USB peripheral。
- 80×160 RGB565 完整畫面是 25,600 bytes，無法放進 SRAM；韌體必須邊收邊寫 LCD。
- 若 USB D+/D- 直接接到 CH32V003 GPIO，推定為軟體 USB Low-Speed。這適合控制與低更新率圖片，不適合 1～30 FPS 的未壓縮全畫面串流。
- 若目標是穩定的螢幕鏡像/影片，建議改用有原生 USB Full-Speed device 的 MCU，並採 Vendor-Specific Bulk；例如 CH32X035、CH32V203，或同級器件。
- 在 CH32V003 上，建議先用 **1 scanline（160 bytes）** 的工作緩衝區。確認 RAM 餘裕後才升為 2 行（320 bytes）；不採原規格例示的 4 行（640 bytes）作為預設。
- USB 傳輸和 LCD SPI 應解耦；主機只在裝置回覆 credit/ready 後傳下一塊，避免 2 KiB SRAM 被佇列吃完。

## 2. MCU 資源限制

CH32V003F4P6 的主要條件：

| 項目 | 資源 | 影響 |
|---|---:|---|
| Flash | 16 KiB | USB stack、協定、LCD driver 必須精簡；不適合複雜 codec |
| SRAM | 2 KiB | 不可配置 25,600-byte framebuffer；USB、stack、全域資料和 block buffer 共用 |
| CPU | RISC-V QingKe V2A，最高 48 MHz | 足以做封包解析、CRC 與 SPI 搬運；軟體 USB 會占用大量即時處理能力 |
| SPI | 1 組 | 專供 ST7735S；可用 DMA，但 CH32V003 僅一組 DMA 資源，需避免與接收流程競用 |
| USB | 無硬體 USB controller | 必須靠 GPIO 軟體實作或板上外接 USB bridge |

建議先預留約 1 KiB 給 stack、USB 狀態及驅動資料，採單一 160-byte payload buffer，加上約 20-byte header/parser 狀態。實際 SRAM 使用量必須由 linker map 驗證，不能只按 C 結構大小估算。

## 3. USB 電路與 Windows 通訊選擇

在沒有原理圖與 PCB 照片前，有三種可能：

1. **D+/D- 經電阻直接接 GPIO**：軟體 USB Low-Speed。可使用 HID interrupt transfer，Windows 免安裝自訂 driver，但吞吐量很低。
2. **板上有 CH340/CH343/CP210x 等 bridge**：Windows 看到 COM port，韌體走 UART；實作簡單，但速率取決於 bridge 與 UART 設定。
3. **其實使用另一顆帶 USB 的 MCU / USB bridge**：才適合 CDC 或 Vendor-Specific Bulk。

介面建議：

| 硬體條件 | 建議 class | 原因 |
|---|---|---|
| CH32V003 GPIO 軟體 USB LS | HID | Low-Speed 沒有 Bulk endpoint；HID 在 Windows 有內建 driver |
| 外接 USB-UART | CDC/虛擬 COM（由 bridge 提供） | Host 開發最簡單，適合先驗證純色與靜態圖 |
| 原生 USB Full-Speed MCU | Vendor-Specific Bulk（首選） | 適合持續串流、封包較大、協定開銷較低；Windows 可用 WinUSB |
| 原生 USB FS、重視簡單安裝 | CDC ACM | 易除錯，但 framing 與驅動行為不如 WinUSB Bulk 可控 |

不建議在軟體 USB Low-Speed 上宣稱 CDC 或 Vendor Bulk：USB Low-Speed 規格不提供 Bulk transfer，CDC 也不是合適的 LS 裝置模型。

## 4. FPS 與瓶頸估算

每個完整 RGB565 frame 是：

```text
80 × 160 × 2 = 25,600 bytes
1 FPS  = 25.6 kB/s（未計封包開銷）
10 FPS = 256 kB/s
30 FPS = 768 kB/s
```

### LCD SPI

若 SPI 有效 clock 為 12 MHz，純 pixel payload 理論約 58.6 FPS；24 MHz 約 117 FPS。加入 ST7735 address-window command、DMA/輪詢間隙及 LCD 模組訊號品質後，工程目標宜先定在 **15～30 FPS**。SPI 不是原生 USB FS 方案的主要瓶頸，但可能是 30 FPS 下的優化點。

### USB

- Low-Speed HID 的 interrupt endpoint payload 很小，實際應以 descriptor 的 polling interval 與實測為準；即使理想化以每毫秒 8 bytes 計，也只有約 8 kB/s，完整畫面低於 1 FPS。
- UART bridge 若使用 921,600 baud、8N1，payload 上限約 92 kB/s，未壓縮全畫面約 3.6 FPS。
- Full-Speed USB Bulk 的實際有效頻寬遠高於 768 kB/s，足以供 30 FPS RGB565；此時 SPI、主機處理與 flow control 才成為主要因素。

所以，現板若是軟體 USB，Phase 2/3 可驗證，但 Phase 4/5 的實用更新率需要更換通訊硬體、加入差分/壓縮，或只傳 dirty rectangles。

## 5. 建議 block 與 flow control

預設採 80×1 pixel：

```text
payload = 80 × 1 × 2 = 160 bytes
```

原因：

- 能保守地留出 SRAM 給 USB stack、呼叫 stack 與驅動。
- 一個 block 可直接設定一次 ST7735 address window 後連續寫入。
- 發生 checksum 錯誤時只需重送一行。

在 native USB FS 且 linker map 證實有空間時，可以改為 80×2（320 bytes）。Host 端的 protocol API 不依賴固定行數，裝置在 `INIT_ACK` 回報 `max_payload`，由 Host 動態選擇。

只允許一個 in-flight pixel block：裝置完成 checksum 驗證並啟動/完成 SPI 後回覆 ACK/credit。後續可加雙緩衝，但不是 CH32V003 的第一版目標。

## 6. Packet Protocol v1 提案

所有多 byte 整數使用 little-endian。RGB565 pixel 以 wire order **MSB first** 傳送，讓 MCU 可直接送入 ST7735；Host 在轉換階段完成 byte swap。

### Request header（固定 16 bytes）

| Offset | Size | 欄位 | 說明 |
|---:|---:|---|---|
| 0 | 2 | Magic | `0x53 0x44` (`SD`) |
| 2 | 1 | Version | `1` |
| 3 | 1 | Command | command enum |
| 4 | 1 | Frame ID | 0～255，自然回繞 |
| 5 | 1 | Flags | bit 0: ACK required；其餘保留 |
| 6 | 1 | X | 0～79 |
| 7 | 1 | Y | 0～159 |
| 8 | 1 | Width | 1～80 |
| 9 | 1 | Height | 1～160，且 payload 不得超過 negotiated max |
| 10 | 2 | Payload length | little-endian |
| 12 | 2 | Sequence | block sequence，little-endian |
| 14 | 2 | Header CRC16 | CRC-16/CCITT-FALSE，涵蓋 bytes 0～13 |

header 後接 payload，最後再接 2-byte payload CRC16。沒有 payload 的 command 不附 payload CRC。

### Commands

```text
0x01 CMD_INIT
0x02 CMD_CLEAR
0x03 CMD_FRAME_BEGIN
0x04 CMD_PIXEL_BLOCK
0x05 CMD_FRAME_END
0x06 CMD_SET_BRIGHTNESS
0x07 CMD_PING
0x80 RSP_ACK
0x81 RSP_NACK
0x82 RSP_CAPABILITIES
0x83 RSP_PONG
```

`CMD_INIT` 觸發 LCD/stream state reset，回覆 capabilities（panel size、max payload、protocol version）。`FRAME_BEGIN` 清除上一幀的 sequence 狀態；`PIXEL_BLOCK` 驗證 bounds、length 與 CRC 後才寫 LCD；`FRAME_END` 回報完整/遺失 block 狀態。錯誤時 NACK 包含 sequence 與 error code，Host 可重送或放棄該 frame。

## 7. Repository architecture

```text
/
├─ README.md
├─ docs/
│  ├─ hardware.md
│  ├─ protocol.md
│  └─ performance.md
├─ host/
│  ├─ MiniDisplay.sln
│  ├─ MiniDisplay.App/                 # .NET 8 WPF、View/ViewModel
│  ├─ MiniDisplay.Core/                # mode/state、frame pipeline abstractions
│  ├─ MiniDisplay.Imaging/             # resize/crop/rotation/RGB565
│  ├─ MiniDisplay.Capture/             # Phase 4: GDI capture，後續 Desktop Duplication
│  ├─ MiniDisplay.Media/               # Phase 5: video/GIF decoder adapter
│  ├─ MiniDisplay.Transport/           # HID/Serial/WinUSB implementations
│  ├─ MiniDisplay.Protocol/            # packet codec、CRC、flow control
│  └─ tests/
│     ├─ MiniDisplay.Imaging.Tests/
│     └─ MiniDisplay.Protocol.Tests/
├─ firmware/
│  ├─ include/
│  │  ├─ st7735.h
│  │  └─ protocol.h
│  ├─ src/
│  │  ├─ main.c
│  │  ├─ st7735.c
│  │  └─ protocol.c
│  ├─ platform/                         # clock/GPIO/SPI/USB-or-UART adapter
│  ├─ tests/                            # host-buildable parser/CRC tests
│  └─ Makefile
└─ tools/
   └─ protocol-vectors/                 # shared binary golden test vectors
```

Transport 必須位於介面後方：`IDisplayTransport` 只提供 connect/send/receive/capabilities。這讓 Phase 2 可先使用 Serial 或 mock transport，之後換 WinUSB 不需修改 imaging 與 UI。

Host pipeline：

```text
Source -> Frame scheduler -> Transform 80×160 -> RGB565 rows
       -> Packet encoder -> flow-controlled transport -> device
```

UI 使用 MVVM。Preview 顯示 transform 後、RGB565 量化前的 80×160 結果；統計由 transport/frame scheduler 提供，不由 View 自行計算。

## 8. 分階段可驗證交付

1. **Phase 1**：建立上述骨架、protocol 文件、C/C# 共用 golden vectors；兩側 CRC/packet tests 可獨立執行。
2. **Phase 2**：Firmware LCD + transport + parser；Host console/WPF 發送紅綠藍白黑；提供 mock transport，無硬體也能測 packet。
3. **Phase 3**：PNG/JPG/BMP、Fit/Fill/Stretch、rotation、RGB565 tests 與 Preview。
4. **Phase 4**：先用 GDI `CopyFromScreen` 與 monitor/rectangle 選擇；量測 dropped frames。
5. **Phase 5**：以 decoder adapter 加 MP4/AVI/GIF，不處理聲音；依目標 FPS 丟棄過期 frame，不累積佇列。
6. **Phase 6**：Desktop Duplication、dirty rectangles、雙緩衝（若 SRAM 足夠）、USB/SPI pipeline 與 profiling。

## 9. 實作前需要確認的硬體資訊

以下資訊會決定 Phase 1/2 的 transport，不能靠軟體猜測：

- USB connector 的 D+、D- 實際接到哪兩個 pin，是否有 1.5 kΩ pull-up、串聯電阻或 USB bridge IC。
- LCD 的 `CS/DC/RST/SCK/MOSI/BL` pin mapping、供電電壓與面板 offset。
- ST7735S 模組允許的 SPI clock（部分便宜模組受排線/level shifter 限制）。
- 是否接受改用原生 USB FS MCU；若不接受，應把目標定位為靜態圖/低更新率資訊面板，而非 30 FPS 影片。

## 10. 建議決策

若必須保留 CH32V003F4P6：以 HID 軟體 USB或板上 UART bridge 做 Phase 2/3，目標 0.2～3 FPS（依實際電路），並優先加入 dirty rectangles。

若需求確實包含流暢 Mirror/Video：改用原生 USB Full-Speed MCU，採 WinUSB Vendor-Specific Bulk，保留 CH32V003 版作低成本變體。這是最能同時滿足 80×160 RGB565、15～30 FPS 與可靠 flow control 的路徑。

## 參考資料

- WCH CH32V003 product page / datasheet / reference manual: <https://www.wch-ic.com/products/CH32V003.html>
- WCH MCU selection table（CH32V003 的 USB2.0 欄為空）: <https://www.wch-ic.com/products/productsCenter/mcuInterface?categoryId=70>
