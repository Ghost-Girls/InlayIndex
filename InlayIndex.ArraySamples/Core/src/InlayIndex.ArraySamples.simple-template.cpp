#include <stdint.h>
#include "../inc/simple-template.h"

enum Color { RED, GREEN, BLUE };
enum Color colors[] = { RED, GREEN, BLUE, RED };


int acs[] = { AAA, BBB, CCC,		DDD, EEE, 12 };

#define FIB_MID 2,3,5,8
#define FIB_ALL  1,1,2,3,5,8,13,21,FIB_MID

int Array7[] = { 1, 1, FIB_ALL, 13, 21 };
int Array8[] = FIB_X;

/*!< report descriptor size */
#define HID_MOUSE_REPORT_DESC_SIZE 74

/*!< hid mouse report descriptor */
static const uint8_t hid_mouse_report_desc[HID_MOUSE_REPORT_DESC_SIZE] = {
	0x05, 0x01, // USAGE_PAGE (Generic Desktop)
	0x09, 0x02, // USAGE (Mouse)
	0xA1, 0x01, // COLLECTION (Application)
	0x09, 0x01, //   USAGE (Pointer)

	0xA1, 0x00, //   COLLECTION (Physical)
	0x05, 0x09, //     USAGE_PAGE (Button)
	0x19, 0x01, //     USAGE_MINIMUM (Button 1)
	0x29, 0x03, //     USAGE_MAXIMUM (Button 3)

	0x15, 0x00, //     LOGICAL_MINIMUM (0)
	0x25, 0x01, //     LOGICAL_MAXIMUM (1)
	0x95, 0x03, //     REPORT_COUNT (3)
	0x75, 0x01, //     REPORT_SIZE (1)

	0x81, 0x02, //     INPUT (Data,Var,Abs)
	0x95, 0x01, //     REPORT_COUNT (1)
	0x75, 0x05, //     REPORT_SIZE (5)
	0x81, 0x01, //     INPUT (Cnst,Var,Abs)

	0x05, 0x01, //     USAGE_PAGE (Generic Desktop)
	0x09, 0x30, //     USAGE (X)
	0x09, 0x31, //     USAGE (Y)
	0x09, 0x38,

	0x15, 0x81, //     LOGICAL_MINIMUM (-127)
	0x25, 0x7F, //     LOGICAL_MAXIMUM (127)
	0x75, 0x08, //     REPORT_SIZE (8)
	0x95, 0x03, //     REPORT_COUNT (2)

	0x81, 0x06, //     INPUT (Data,Var,Rel)
	0xC0, 0x09,
	0x3c, 0x05,
	0xff, 0x09,

	0x01, 0x15,
	0x00, 0x25,
	0x01, 0x75,
	0x01, 0x95,

	0x02, 0xb1,
	0x22, 0x75,
	0x06, 0x95,
	0x01, 0xb1,

	0x01, 0xc0 //   END_COLLECTION
};