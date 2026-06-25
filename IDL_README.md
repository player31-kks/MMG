1. 전역 설정

파일 최상단에 반드시 존재.

//+PACK_SIZE=1
//+MOST_BYTE=true
설정	의미
PACK_SIZE	구조체 패킹 크기
MOST_BYTE=true	Big Endian
MOST_BYTE=false	Little Endian
2. 헤더 구조체
반드시 1개만 존재
struct 선언부에 // $()$
메시지 식별 필드에도 // $()$

예:

struct MsgHeader // $()$
{
    unsigned short MsgID; // $()$
    unsigned short Length;
};
3. 메시지 구조체
struct 선언부에 메시지 ID 마커 필요
모든 메시지 ID는 유일해야 함
첫 번째 필드는 반드시 헤더 타입

예:

struct Msg_Status // $(100)$
{
    MsgHeader header;
};

허용:

// $(100)$
// $(0x64)$
// $(0xA2)$

금지:

struct Msg_Status
{
    unsigned int timestamp;
    MsgHeader header;
};
4. 사용자 정의 구조체(Sub Struct)
메시지 내부에서 재사용 가능
반드시 사용 전에 정의되어 있어야 함

허용:

struct SensorData
{
    int temp;
};

struct Msg_Status // $(100)$
{
    MsgHeader header;
    SensorData sensor;
};

금지:

struct Msg_Status // $(100)$
{
    MsgHeader header;
    SensorData sensor;
};

struct SensorData
{
    int temp;
};
5. 필드 문법
스칼라
int value;
float voltage;
double current;
배열
unsigned char payload[32];
비트필드
unsigned int mode : 3;
unsigned int flag : 1;
6. 지원 타입

기본 C 타입:

char
unsigned char
short
unsigned short
int
unsigned int
float
double

Wireshark Lua Script Generator 모드에서는 추가 지원:

int8_t
uint8_t
int16_t
uint16_t
int32_t
uint32_t
int64_t
uint64_t
7. 문법 제약

반드시:

struct Name
{
    int a;
};

금지:

typedef struct
{
    int a;
} Name;

struct Name {
    int a
}

또한:

모든 필드는 ; 로 종료
식별자는 [A-Za-z0-9_]
함수 선언 금지
초기값 지정 금지
포인터 금지
enum 금지
union 금지
typedef 금지
pragma pack 금지
8. 파서 입장에서 필요한 정보
Header 정보
HeaderType = MsgHeader
MsgIdField = MsgID
Message Registry
100 -> Msg_Status
101 -> Msg_Command
102 -> Msg_Report
...
Endian
BigEndian = true
Pack Size
Pack = 1

추가로 추천하는 규칙들이 몇 개 있어.

11. 메시지 ID 중복 금지
Duplicate MsgID -> Parse Error
12. Header는 직접 상속 불가

허용:

MsgHeader header;

금지:

struct Msg_Status : MsgHeader
{
};
13. 배열 길이는 상수 정수만 허용

허용:

unsigned char payload[128];

금지:

unsigned char payload[MAX_SIZE];
unsigned char payload[len];
14. 메시지 ID 범위 정의

예:

0~999     : 공통 메시지
1000~1999 : 유도탄 제어기
2000~2999 : 발사통 제어기
3000~3999 : GUI

이렇게 하면 대규모 시스템에서 충돌을 막기 쉬워.

결론적으로 나는 앞으로 이 포맷을 보면 다음과 같이 인식할게.

//+PACK_SIZE → 구조체 패킹 설정
//+MOST_BYTE → 엔디안 설정
// $()$ on struct → 헤더 구조체
// $()$ on field → 메시지 식별 필드
// $(100)$ → 메시지 ID
첫 필드는 반드시 헤더
이전에 정의된 struct만 참조 가능
C 스타일 선언만 허용

앞으로 이 규칙 기반의 프로토콜 정의 파일을 보여주면 이 규칙을 기준으로 검증하거나 코드 생성 방향을 같이 고민할 수 있어.