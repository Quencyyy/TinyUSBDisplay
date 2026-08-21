#include "display_service.h"

static display_reply_t reply(display_reply_kind_t kind, const md_packet_t *packet,
                             md_result_t error) {
    display_reply_t result = { kind, error, packet->frame_id, packet->sequence };
    return result;
}

void display_service_init(display_service_t *service, st7735_t *lcd) {
    service->lcd = lcd;
    service->active_frame = 0;
    service->next_sequence = 0;
    service->frame_open = false;
}

display_reply_t display_service_handle(display_service_t *service,
                                       const md_packet_t *packet) {
    if (!service || !service->lcd || !packet)
        return (display_reply_t){ DISPLAY_REPLY_NACK, MD_ERR_ARGUMENT, 0, 0 };
    switch (packet->command) {
    case MD_CMD_INIT:
        service->frame_open = false;
        return reply(DISPLAY_REPLY_CAPABILITIES, packet, MD_OK);
    case MD_CMD_CLEAR:
        if (packet->payload_length != 2 ||
            !st7735_clear(service->lcd, (uint16_t)((uint16_t)packet->payload[0] << 8 |
                                                   packet->payload[1])))
            return reply(DISPLAY_REPLY_NACK, packet, MD_ERR_LENGTH);
        return reply(DISPLAY_REPLY_ACK, packet, MD_OK);
    case MD_CMD_FRAME_BEGIN:
        service->active_frame = packet->frame_id;
        service->next_sequence = 0;
        service->frame_open = true;
        return reply(DISPLAY_REPLY_ACK, packet, MD_OK);
    case MD_CMD_PIXEL_BLOCK:
        if (!service->frame_open || packet->frame_id != service->active_frame ||
            packet->sequence != service->next_sequence)
            return reply(DISPLAY_REPLY_NACK, packet, MD_ERR_ARGUMENT);
        if (!st7735_write_block(service->lcd, packet->x, packet->y,
                                packet->width, packet->height,
                                packet->payload, packet->payload_length))
            return reply(DISPLAY_REPLY_NACK, packet, MD_ERR_BOUNDS);
        ++service->next_sequence;
        return reply(DISPLAY_REPLY_ACK, packet, MD_OK);
    case MD_CMD_FRAME_END:
        if (!service->frame_open || packet->frame_id != service->active_frame)
            return reply(DISPLAY_REPLY_NACK, packet, MD_ERR_ARGUMENT);
        service->frame_open = false;
        return reply(DISPLAY_REPLY_ACK, packet, MD_OK);
    case MD_CMD_PING:
        return reply(DISPLAY_REPLY_PONG, packet, MD_OK);
    default:
        return reply(DISPLAY_REPLY_NACK, packet, MD_ERR_ARGUMENT);
    }
}
