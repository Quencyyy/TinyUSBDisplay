#ifndef MINIDISPLAY_PROTOCOL_H
#define MINIDISPLAY_PROTOCOL_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#define MD_PROTOCOL_VERSION 1u
#define MD_HEADER_SIZE 16u
#define MD_MAGIC_0 0x53u
#define MD_MAGIC_1 0x44u
#define MD_MAX_BLOCK_PAYLOAD 640u
#define MD_MAX_PACKET_SIZE (MD_HEADER_SIZE + MD_MAX_BLOCK_PAYLOAD + 2u)

typedef enum {
    MD_CMD_INIT = 0x01,
    MD_CMD_CLEAR = 0x02,
    MD_CMD_FRAME_BEGIN = 0x03,
    MD_CMD_PIXEL_BLOCK = 0x04,
    MD_CMD_FRAME_END = 0x05,
    MD_CMD_SET_BRIGHTNESS = 0x06,
    MD_CMD_PING = 0x07,
    MD_RSP_ACK = 0x80,
    MD_RSP_NACK = 0x81,
    MD_RSP_CAPABILITIES = 0x82,
    MD_RSP_PONG = 0x83
} md_command_t;

typedef enum {
    MD_OK = 0,
    MD_ERR_ARGUMENT,
    MD_ERR_TOO_SHORT,
    MD_ERR_MAGIC,
    MD_ERR_VERSION,
    MD_ERR_HEADER_CRC,
    MD_ERR_LENGTH,
    MD_ERR_PAYLOAD_CRC,
    MD_ERR_BOUNDS
} md_result_t;

typedef struct {
    uint8_t command;
    uint8_t frame_id;
    uint8_t flags;
    uint8_t x;
    uint8_t y;
    uint8_t width;
    uint8_t height;
    uint16_t payload_length;
    uint16_t sequence;
    const uint8_t *payload;
} md_packet_t;

uint16_t md_crc16_ccitt_false(const uint8_t *data, size_t length);
size_t md_encoded_size(uint16_t payload_length);
md_result_t md_encode(const md_packet_t *packet, uint8_t *output,
                      size_t capacity, size_t *written);
md_result_t md_decode(const uint8_t *input, size_t length,
                      md_packet_t *packet, size_t *consumed);

#endif
