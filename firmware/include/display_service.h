#ifndef MINIDISPLAY_DISPLAY_SERVICE_H
#define MINIDISPLAY_DISPLAY_SERVICE_H

#include "protocol.h"
#include "st7735.h"

typedef enum {
    DISPLAY_REPLY_ACK,
    DISPLAY_REPLY_NACK,
    DISPLAY_REPLY_CAPABILITIES,
    DISPLAY_REPLY_PONG
} display_reply_kind_t;

typedef struct {
    display_reply_kind_t kind;
    md_result_t error;
    uint8_t frame_id;
    uint16_t sequence;
} display_reply_t;

typedef struct {
    st7735_t *lcd;
    uint8_t active_frame;
    uint16_t next_sequence;
    bool frame_open;
} display_service_t;

void display_service_init(display_service_t *service, st7735_t *lcd);
display_reply_t display_service_handle(display_service_t *service,
                                       const md_packet_t *packet);

#endif
