// Array Inline Index 插件测试文件
// 包含各种数组初始化样式（新增宏定义数组测试）

// ==================== 一维数组 ====================

// 简单一维数组
int Array1[3] = { 1, 2, 3 };
int Array2[5] = { 10, 20, 30, 40, 50 };
char Array3[4] = { 'a', 'b', 'c', 'd' };
float Array4[6] = { 1.1, 2.2, 3.3, 4.4, 5.5, 6.6 };
double Array5[8] = { 1.11, 2.22, 3.33, 4.44, 5.55, 6.66, 7.77, 8.88 };

// 自动推断大小
int Array6[] = { 100, 200, 300, 400, 500 };
const int Array7[] = { 1, 1, 2, 3, 5, 8, 13, 21 };

// 大数组（50 个元素）
int Array8[50] = {
	1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
	11, 12, 13, 14, 15, 16, 17, 18, 19, 20,
	21, 22, 23, 24, 25, 26, 27, 28, 29, 30,
	31, 32, 33, 34, 35, 36, 37, 38, 39, 40,
	41, 42, 43, 44, 45, 46, 47, 48, 49, 50
};

// 超大数组（100 个元素）
int Array9[100] = {
	1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
	11, 12, 13, 14, 15, 16, 17, 18, 19, 20,
	21, 22, 23, 24, 25, 26, 27, 28, 29, 30,
	31, 32, 33, 34, 35, 36, 37, 38, 39, 40,
	41, 42, 43, 44, 45, 46, 47, 48, 49, 50,
	51, 52, 53, 54, 55, 56, 57, 58, 59, 60,
	61, 62, 63, 64, 65, 66, 67, 68, 69, 70,
	71, 72, 73, 74, 75, 76, 77, 78, 79, 80,
	81, 82, 83, 84, 85, 86, 87, 88, 89, 90,
	91, 92, 93, 94, 95, 96, 97, 98, 99, 100
};

// ==================== 二维数组 ====================

// 简单二维数组
int Matrix1[2][3] = {
	{ 1, 2, 3 },
	{ 4, 5, 6 }
};

int Matrix2[3][3] = {
	{ 1, 2, 3 },
	{ 4, 5, 6 },
	{ 7, 8, 9 }
};

// 单位矩阵
int Identity[3][3] = {
	{ 1, 0, 0 },
	{ 0, 1, 0 },
	{ 0, 0, 1 }
};

// 大二维数组
int Matrix3[5][5] = {
	{ 1, 2, 3, 4, 5 },
	{ 6, 7, 8, 9, 10 },
	{ 11, 12, 13, 14, 15 },
	{ 16, 17, 18, 19, 20 },
	{ 21, 22, 23, 24, 25 }
};

// 字符矩阵
char CharMatrix[3][4] = {
	{ 'a', 'b', 'c', 'd' },
	{ 'e', 'f', 'g', 'h' },
	{ 'i', 'j', 'k', 'l' }
};

// ==================== 三维数组 ====================

// 简单三维数组
int Cube1[2][2][2] = {
	{
		{ 1, 2 },
		{ 3, 4 }
	},
	{
		{ 5, 6 },
		{ 7, 8 }
	}
};

int Cube2[2][3][4] = {
	{
		{ 1, 2, 3, 4 },
		{ 5, 6, 7, 8 },
		{ 9, 10, 11, 12 }
	},
	{
		{ 13, 14, 15, 16 },
		{ 17, 18, 19, 20 },
		{ 21, 22, 23, 24 }
	}
};

// ==================== 四维数组 ====================

int HyperCube[2][2][2][2] = {
	{
		{
			{ 1, 2 },
			{ 3, 4 }
		},
		{
			{ 5, 6 },
			{ 7, 8 }
		}
	},
	{
		{
			{ 9, 10 },
			{ 11, 12 }
		},
		{
			{ 13, 14 },
			{ 15, 16 }
		}
	}
};

// ==================== 混合初始化 ====================

// 部分指定初始化
int Partial1[5] = { 1, 2, 3 };
int Partial2[10] = { 100, 200 };

// 带省略号的初始化
int Sparse[10] = { 1, 0, 0, 0, 5, 0, 0, 0, 0, 10 };

// 嵌套和扁平混合
int Mixed1[2][3] = {
	1, 2, 3, 4, 5, 6
};

int Mixed2[2][3] = {
	{ 1, 2 }, 3, 4, 5, 6
};

// ==================== 结构体数组 ====================

struct Point {
	int x;
	int y;
};

struct Point pointxy[3] = {
	{ 1, 2 },
	{ 3, 4 },
	{ 5, 6 }
};

struct PointXYZ {
	int x;
	int y;
	int z;
};

struct PointXYZ pointxyz[3] = {
	{ 1, 2 ,3},
	{ 4, 5 ,6},
	{ 7, 8, 9}
};

// ==================== 字符串数组 ====================

const char* strings[] = {
	"Hello",
	"World",
	"Test",
	"Array"
};

// ==================== 枚举和常量 ====================

enum Color { RED, GREEN, BLUE };
enum Color colors[] = { RED, GREEN, BLUE, RED, BLUE };

// ==================== 复杂表达式 ====================

int Expr[5] = { 1 + 2, 3 * 4, 10 - 5, 8 / 2, 15 % 4 };

// ==================== 多维大数组 ====================

int Large3D[4][4][4] = {
	{
		{ 1, 2, 3, 4 },
		{ 5, 6, 7, 8 },
		{ 9, 10, 11, 12 },
		{ 13, 14, 15, 16 }
	},
	{
		{ 17, 18, 19, 20 },
		{ 21, 22, 23, 24 },
		{ 25, 26, 27, 28 },
		{ 29, 30, 31, 32 }
	},
	{
		{ 33, 34, 35, 36 },
		{ 37, 38, 39, 40 },
		{ 41, 42, 43, 44 },
		{ 45, 46, 47, 48 }
	},
	{
		{ 49, 50, 51, 52 },
		{ 53, 54, 55, 56 },
		{ 57, 58, 59, 60 },
		{ 61, 62, 63, 64 }
	}
};

// ==================== 宏定义数组（新增核心测试用例） ====================

// 1. 基础宏定义数组（无参数）
#define BASIC_ARRAY_INIT 10, 20, 30, 40
int MacroArray1[4] = { BASIC_ARRAY_INIT };

// 2. 带参数的宏初始化数组（模拟USB描述符场景）
#define WBVAL(x) ((x) & 0xFF), (((x) >> 8) & 0xFF) // 字节拆分宏
#define USB_DESCRIPTOR_TYPE_DEVICE 0x01
#define USB_STRING_MFC_INDEX 0x01
#define USB_STRING_PRODUCT_INDEX 0x02
#define USB_STRING_SERIAL_INDEX 0x03
#define USB_2_0 0x0200
#define USBD_VID 0x1234
#define USBD_PID 0x5678

// 复杂参数化宏（USB设备描述符）
#define USB_DEVICE_DESCRIPTOR_INIT(bcdUSB, bDeviceClass, bDeviceSubClass, bDeviceProtocol, idVendor, idProduct, bcdDevice, bNumConfigurations)	\
    0x12,                       /* bLength */																										\
    USB_DESCRIPTOR_TYPE_DEVICE, /* bDescriptorType */																								\
    WBVAL(bcdUSB),              /* bcdUSB */																										\
    bDeviceClass,               /* bDeviceClass */																									\
    bDeviceSubClass,            /* bDeviceSubClass */																								\
    bDeviceProtocol,            /* bDeviceProtocol */																								\
    0x40,                       /* bMaxPacketSize */																								\
    WBVAL(idVendor),            /* idVendor */																									\
    WBVAL(idProduct),           /* idProduct */																									\
    WBVAL(bcdDevice),           /* bcdDevice */																									\
    USB_STRING_MFC_INDEX,       /* iManufacturer */																								\
    USB_STRING_PRODUCT_INDEX,   /* iProduct */																									\
    USB_STRING_SERIAL_INDEX,    /* iSerial */																										\
    bNumConfigurations          /* bNumConfigurations */

// 宏展开的字节数组（核心测试用例）
static const unsigned int device_descriptor[] = {
	USB_DEVICE_DESCRIPTOR_INIT(USB_2_0, 0x00, 0x00, 0x00, USBD_VID, USBD_PID, 0x0002, 0x01)
};

// 3. 嵌套宏数组
#define NESTED_MACRO1 WBVAL(0x1001)
#define NESTED_MACRO2 0x05, NESTED_MACRO1, 0x06
unsigned int NestedMacroArray[5] = { NESTED_MACRO2, 0x07 };

// 4. 宏定义数组大小
#define ARRAY_SIZE 8
int MacroSizeArray[ARRAY_SIZE] = { 1, 2, 3, 4, 5, 6, 7, 8 };

// 5. 宏条件编译数组
#ifdef _DEBUG
#define DEBUG_ARRAY_INIT 0xFF, 0xFF, 0xFF
#else	
#define DEBUG_ARRAY_INIT 0x00, 0x00, 0x00
#endif
unsigned int CondMacroArray[3] = { DEBUG_ARRAY_INIT };

// 6. 宏展开的二维数组
#define ROW_INIT(x) {x, x+1, x+2}
int Macro2DArray[3][3] = {
	ROW_INIT(1),
	ROW_INIT(4),
	ROW_INIT(7)
};

// 7. 变长宏（C99+）
#define VAR_ARGS_ARRAY(...) {__VA_ARGS__}
int VarArgsArray[] = VAR_ARGS_ARRAY(100, 200, 300, 400, 500);

// ==================== 主函数 ====================

int main()
{
	// 局部数组
	int local[5] = { 1, 2, 3, 4, 5 };
	int local2D[2][2] = {
		{ 10, 20 },
		{ 30, 40 }
	};

	// 局部宏数组
#define LOCAL_MACRO 9, 8, 7
	int localMacroArray[3] = { LOCAL_MACRO };

	return 0;
}