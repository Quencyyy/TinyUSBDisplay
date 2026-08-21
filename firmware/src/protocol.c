#include "protocol.h"

static uint16_t read_u16_le(const uint8_t *p) {
    return (uint16_t)p[0] | ((uint16_t)p[1] << 8);
}

static void write_u16_le(uint8_t *p, uint16_t value) {
    p[0] = (uint8_t)value;
    p[1] = (uint8_t)(value >> 8);
}

uint16_t md_crc16_ccitt_false(const uint8_t *data, size_t length) {
    uint16_t crc = 0xFFFFu;
    size_t i;
    for (i = 0; i < length; ++i) {
        uint8_t bit;
        crc ^= (uint16_t)data[i] << 8;
        for (bit = 0; bit < 8; ++bit) {
            crc = (crc & 0x8000u) ? (uint16_t)((crc << 1) ^ 0x1021u)
                                  : (uint16_t)(crc << 1);
        }
    }
    return crc;
}

size_t md_encoded_size(uint16_t payload_length) {
    return MD_HEADER_SIZE + payload_length + (payload_length ? 2u : 0u);
}

static md_result_t validate_fields(const md_packet_t *packet) {
    if (packet->payload_length > MD_MAX_BLOCK_PAYLOAD) return MD_ERR_LENGTH;
    if (packet->payload_length && packet->payload == NULL) return MD_ERR_ARGUMENT;
    if (packet->command == MD_CMD_PIXEL_BLOCK) {
        uint32_t expected;
        if (packet->width == 0 || packet->height == 0 ||
            packet->x >= 80 || packet->y >= 160 ||
            (uint16_t)packet->x + packet->width > 80 ||
            (uint16_t)packet->y + packet->height > 160) return MD_ERR_BOUNDS;
        expected = (uint32_t)packet->width * packet->height * 2u;
        if (expected != packet->payload_length) return MD_ERR_LENGTH;
    }
    return MD_OK;
}

md_result_t md_encode(const md_packet_t *packet, uint8_t *output,
                      size_t capacity, size_t *written) {
    md_result_t result;
    size_t total;
    uint16_t i;
    if (!packet || !output || !written) return MD_ERR_ARGUMENT;
    result = validate_fields(packet);
    if (result != MD_OK) return result;
    total = md_encoded_size(packet->payload_length);
    if (capacity < total) return MD_ERR_TOO_SHORT;
    output[0] = MD_MAGIC_0; output[1] = MD_MAGIC_1;
    output[2] = MD_PROTOCOL_VERSION; output[3] = packet->command;
    output[4] = packet->frame_id; output[5] = packet->flags;
    output[6] = packet->x; output[7] = packet->y;
    output[8] = packet->width; output[9] = packet->height;
    write_u16_le(output + 10, packet->payload_length);
    write_u16_le(output + 12, packet->sequence);
    write_u16_le(output + 14, md_crc16_ccitt_false(output, 14));
    for (i = 0; i < packet->payload_length; ++i) output[16u + i] = packet->payload[i];
    if (packet->payload_length) {
        write_u16_le(output + 16u + packet->payload_length,
                     md_crc16_ccitt_false(packet->payload, packet->payload_length));
    }
    *written = total;
    return MD_OK;
}

md_result_t md_decode(const uint8_t *input, size_t length,
                      md_packet_t *packet, size_t *consumed) {
    uint16_t payload_length;
    size_t total;
    md_result_t result;
    if (!input || !packet || !consumed) return MD_ERR_ARGUMENT;
    if (length < MD_HEADER_SIZE) return MD_ERR_TOO_SHORT;
    if (input[0] != MD_MAGIC_0 || input[1] != MD_MAGIC_1) return MD_ERR_MAGIC;
    if (input[2] != MD_PROTOCOL_VERSION) return MD_ERR_VERSION;
    if (read_u16_le(input + 14) != md_crc16_ccitt_false(input, 14)) return MD_ERR_HEADER_CRC;
    payload_length = read_u16_le(input + 10);
    total = md_encoded_size(payload_length);
    if (payload_length > MD_MAX_BLOCK_PAYLOAD) return MD_ERR_LENGTH;
    if (length < total) return MD_ERR_TOO_SHORT;
    if (payload_length && read_u16_le(input + 16u + payload_length) !=
        md_crc16_ccitt_false(input + 16, payload_length)) return MD_ERR_PAYLOAD_CRC;
    packet->command = input[3]; packet->frame_id = input[4]; packet->flags = input[5];
    packet->x = input[6]; packet->y = input[7]; packet->width = input[8]; packet->height = input[9];
    packet->payload_length = payload_length; packet->sequence = read_u16_le(input + 12);
    packet->payload = payload_length ? input + 16 : NULL;
    result = validate_fields(packet);
    if (result != MD_OK) return result;
    *consumed = total;
    return MD_OK;
}
