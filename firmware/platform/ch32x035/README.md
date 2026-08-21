# CH32X035 platform adapter

Phase 2 core is complete, but the board image cannot be produced until the WCH RISC-V toolchain/MounRiver Studio is installed. This adapter must bind:

- PA5: SPI1 SCLK
- PA7: SPI1 MOSI
- PA6: GPIO D/C
- PA0: GPIO RESET
- PC3: GPIO CS
- PC16/PC17: USBFS D-/D+

Use the official `openwch/ch32x035` USBFS device implementation as the register-level base. Configure a vendor-specific interface with EP1 OUT (64 bytes) and EP2 IN (64 bytes). EP1 OUT packets feed a bounded stream assembler; complete protocol packets are decoded with `md_decode`, dispatched through `display_service_handle`, and acknowledged over EP2 IN.

Do not call `display_service_handle` directly from the USB interrupt: enqueue/copy each 64-byte USB transaction into the stream assembler, NAK OUT while a complete 640-byte block is being written over SPI, then restore ACK.
