# Mini Display Protocol v1

Transport is an ordered, reliable byte stream over USB Bulk. Multi-byte header integers and CRC values are little-endian. RGB565 pixels are sent high byte first so the MCU can forward payload directly to ST7735S.

## Request frame

```text
16-byte header | payload (0..negotiated max) | payload CRC16 (only if payload exists)
```

| Offset | Size | Field |
|---:|---:|---|
| 0 | 2 | Magic: `53 44` (`SD`) |
| 2 | 1 | Version: `01` |
| 3 | 1 | Command |
| 4 | 1 | Frame ID |
| 5 | 1 | Flags; bit 0 means ACK required |
| 6 | 1 | X |
| 7 | 1 | Y |
| 8 | 1 | Width |
| 9 | 1 | Height |
| 10 | 2 | Payload length, LE |
| 12 | 2 | Sequence, LE |
| 14 | 2 | Header CRC16 over bytes 0..13, LE |

Payload CRC is CRC16 over payload only. CRC algorithm is CRC-16/CCITT-FALSE: polynomial `0x1021`, initial value `0xFFFF`, no reflection, xor-out `0x0000`.

## Commands

| Value | Name | Payload |
|---:|---|---|
| 0x01 | INIT | none |
| 0x02 | CLEAR | 2-byte RGB565 colour |
| 0x03 | FRAME_BEGIN | none |
| 0x04 | PIXEL_BLOCK | `width × height × 2` bytes |
| 0x05 | FRAME_END | none |
| 0x06 | SET_BRIGHTNESS | one byte, 0..255 |
| 0x07 | PING | optional opaque bytes |
| 0x80 | ACK | status payload |
| 0x81 | NACK | error payload |
| 0x82 | CAPABILITIES | capability payload |
| 0x83 | PONG | echoed payload |

## Validation

Receiver rejects a frame when magic/version, header CRC, payload CRC, coordinate bounds, negotiated maximum, or `PIXEL_BLOCK` payload length is invalid. Parser resynchronizes by scanning for the two magic bytes. No packed C struct is placed directly on the wire.

Host sends at most two pixel blocks without returned credit in the initial implementation. `FRAME_END` is not considered displayed until every preceding block has been committed to SPI.
