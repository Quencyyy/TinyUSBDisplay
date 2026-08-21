# Hardware baseline

## Board identification

商品接線圖上的 U1 為 `CH32X033F8P6`，屬於 WCH CH32X035 系列。原廠系列資料列出 48 MHz RISC-V4C、62 KiB Flash、20 KiB SRAM、8-channel DMA，以及 USB 2.0 Full-Speed controller/PHY。

這與最初需求中的 CH32V003 有實質差異：X033 有原生 USB FS，20 KiB SRAM 也足以配置兩個小 block buffer，但 25,600-byte RGB565 framebuffer 仍不應放在 MCU。

## Connections transcribed from supplied diagram

| Function | MCU pin | Note |
|---|---|---|
| LCD SCLK | PA5 | SPI_SCLK |
| LCD MOSI/SDA | PA7 | SPI_MOSI |
| LCD D/C | PA6 | schematic net is labelled SPI_MISO but connects to D/C |
| LCD RESET | PA0 | RSET |
| LCD CS | PC3 | SPI_CS_LCD |
| USB D- | dedicated UDM pin | diagram routes connector D- directly to MCU |
| USB D+ | dedicated UDP pin | diagram routes connector D+ directly to MCU |
| Flash CS | PB7 | external P25D80H |
| Flash SCLK/MOSI/MISO | PA5/PA7/PA6 | shared SPI bus; individual CS |

PA6 is shared with flash MISO but used as LCD D/C. This is electrically plausible only when flash CS is inactive; firmware must never change D/C during a flash read. The display streaming firmware should leave external flash deselected.

## Transport decision

Use USB Full-Speed Vendor-Specific Bulk with WinUSB:

- OUT endpoint: Host packets and RGB565 blocks.
- IN endpoint: capabilities, ACK/NACK, status.
- Endpoint max packet size: 64 bytes.
- Initial application block: 80×4 pixels = 640 bytes.
- Two 640-byte buffers are acceptable with 20 KiB SRAM, subject to linker-map verification in Phase 2.

At 30 FPS, raw pixels require 768 kB/s plus protocol overhead, within practical USB FS Bulk throughput. A 12 MHz SPI takes about 17 ms per frame of pixel data; 24 MHz takes about 8.5 ms. Start at 12 MHz and target 20 FPS, then qualify 24 MHz and 30 FPS on the actual module.

## References

- WCH CH32X035 product page: <https://www.wch-ic.com/products/CH32X035.html>
- WCH CH32X035 datasheet: <https://www.wch-ic.com/downloads/CH32X035DS0_PDF.html>
