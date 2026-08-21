# Protocol test vectors

The canonical CRC check vector is ASCII `123456789` -> CRC-16/CCITT-FALSE `0x29B1`.

The C and C# self-tests both encode this logical packet and require an identical 178-byte result:

```text
command=PIXEL_BLOCK, frame=7, flags=ACK_REQUIRED
x=0, y=42, width=80, height=1, sequence=12
payload=80 red RGB565 pixels (F8 00 repeated)
```

Phase 2 will export the encoded binary produced by both implementations and compare it byte-for-byte in CI.
