# Windows용 Valkey

[![Build](https://github.com/valkey-windows/valkey-windows/actions/workflows/build-valkey.yml/badge.svg)](https://github.com/valkey-windows/valkey-windows/actions)
[![Release](https://img.shields.io/github/v/release/valkey-windows/valkey-windows)](https://github.com/valkey-windows/valkey-windows/releases)

공식 Valkey 소스를 기반으로 빌드한 Windows 전용 버전입니다.

## 빠른 시작

```cmd
# 다운로드 후 압축을 풀고 바로 실행
valkey-server.exe valkey.conf

# 또는 ValkeyService 사용(추천)
ValkeyService.exe run --foreground
```

## 사용 방법

### 방법 1: ValkeyService.exe(추천)

경로 변환을 자동으로 처리하며 Windows 네이티브 경로를 지원합니다.

```cmd
# 포그라운드 실행
ValkeyService.exe run --foreground --port 6379 --dir C:\\valkey-data

# Windows 서비스로 설치
ValkeyService.exe install -c C:\\config\\valkey.conf --dir D:\\data\\valkey --port 6379
net start Valkey

# 서비스 제거
ValkeyService.exe uninstall
```

### 방법 2: valkey-server.exe(직접 실행)

**중요:** 이 버전은 Cygwin 런타임을 사용하므로 명령줄 경로는 반드시 Cygwin 형식을 사용해야 합니다.

```cmd
# ✅ 올바름 - Cygwin 경로 형식
valkey-server.exe /cygdrive/c/config/valkey.conf --dir /cygdrive/d/data --port 6379

# ❌ 잘못됨 - Windows 경로는 지원되지 않음
valkey-server.exe C:\\config\\valkey.conf --dir D:\\data
```

**경로 변환 규칙:**

| Windows | Cygwin |
|---------|--------|
| `C:\\path` | `/cygdrive/c/path` |
| `D:\\path` | `/cygdrive/d/path` |
| `.\\data` | `./data`(상대 경로는 그대로 사용) |

**설정 파일에서는:** Windows 스타일의 슬래시(`/`) 경로를 그대로 사용할 수 있습니다.

```conf
# valkey.conf 권장 예시
dir C:/valkey/data
logfile C:/valkey/logs/valkey.log
```

## ValkeyService 명령어 참고

```cmd
ValkeyService.exe [command] [options]

Commands:
  install       Windows 서비스로 설치
  uninstall     Windows 서비스 제거
  run           Valkey 실행(기본)

Options:
  -c, --config <FILE>      설정 파일 경로
  --port <PORT>            포트 번호
  --dir <DIRECTORY>        데이터 디렉터리
  --loglevel <LEVEL>       로그 레벨(debug/verbose/notice/warning)
  -f, --foreground         포그라운드 실행
  --service-name <NAME>    서비스 이름(기본: Valkey)
  --start-mode <MODE>      시작 유형(auto/manual)
  -h, --help               도움말 표시
  -v, --version            버전 표시
```

## 드라이브/디렉터리 분리 구성

설정 파일, 데이터 디렉터리, 프로그램은 어느 위치든 가능합니다:

```cmd
# 프로그램: C:\\valkey\\ValkeyService.exe
# 설정: D:\\config\\valkey.conf
# 데이터: E:\\data\\valkey

ValkeyService.exe run -c D:\\config\\valkey.conf --dir E:\\data\\valkey --foreground
```

## 데이터 영속성

Valkey 종료 시 데이터가 자동 저장됩니다. `ValkeyService.exe` 는 `--dir` 인자를 올바르게 전달하여 지정한 디렉터리에 데이터가 저장되도록 합니다.

```cmd
# 시작
ValkeyService.exe run --foreground --dir C:\\valkey-data

# 데이터 쓰기
valkey-cli SET mykey myvalue

# 우아하게 종료
valkey-cli SHUTDOWN

# 재시작 후에도 데이터 유지
valkey-cli GET mykey   # "myvalue" 반환
```

## 자주 묻는 질문

### valkey-server.exe 가 설정 파일을 찾지 못하나요?

다음과 같이 Cygwin 경로 형식을 사용하세요:
```cmd
valkey-server.exe /cygdrive/c/config/valkey.conf
```

또는 `ValkeyService.exe` 를 사용하면 경로 변환을 자동으로 처리합니다.

### 데이터가 유실되었나요?

1. `--dir` 인자를 지정했는지 확인하세요
2. `valkey-cli SHUTDOWN` 또는 `Ctrl+C` 로 안전하게 종료하고 강제 종료는 피하세요
3. 재시작 시 동일한 `--dir` 인자를 사용하세요

## 기술 세부 정보

- 빌드 도구: MSYS2 / Cygwin
- 서비스 래퍼: .NET 10.0
- 경로 변환: ValkeyService가 Windows ↔ Cygwin 변환을 자동 처리

---

[English](README.md) | 한국어

## 면책 조항

이 프로젝트는 LF Projects, LLC.와 무관합니다. 제공되는 라이선스는 이 저장소의 콘텐츠에만 적용되며 공식 Valkey 프로젝트에는 적용되지 않습니다.

본 프로젝트는 로컬 개발 환경에서만 사용하는 것을 권장합니다. 프로덕션 환경에서는 공식 Valkey 가이드를 따라 Linux에 배포하세요. 이 프로젝트 사용으로 발생하는 손실에 대해 책임지지 않습니다.
