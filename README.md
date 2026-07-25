# TaggedUnion

`TaggedUnion`은 C#에서 구별된 유니온(discriminated union)을 만들어주는 소스 제네레이터입니다.

## 패키지 만들기

```shell
dotnet pack ./TaggedUnion/TaggedUnion.csproj -c Release
```

`./TaggedUnion/bin/Release/` 폴더에 `nupkg` 확장자를 가지는 패키지가 생성됩니다.

### 템플릿을 사용하는 코드

`ITaggedUnionCases<T1, ...>`, `ITaggedUnion<T1, ...>`, `TaggedUnion<T1, ...>` 선언은 T4 템플릿에서 생성합니다. 템플릿을 수정한 후 저장소 루트에서 다음 명령을 실행합니다.

```shell
dotnet run --file ./scripts/GenerateTaggedUnionSources.cs
```

빌드, 패키지 생성 시에는 이 명령을 자동으로 실행하지 않기 때문에 수정 후 직접 호출해야 합니다.

## 사용법

유니온으로 사용할 타입을 `partial struct`로 선언하고 `TaggedUnion` 어트리뷰트를 적용합니다. `record struct`는 지원하지 않습니다. 포함될 타입은 `ITaggedUnionCases<T1, ...>` 인터페이스의 타입 인자로 지정하며, 유니온 타입 자기 자신은 케이스 타입으로 지정할 수 없습니다. `TaggedUnion`이 선언되는 타입은 제네릭이 아니어야 하고 인스턴스 생성자를 가질 수 없습니다.

```csharp
using Macaron.Union;

[TaggedUnion]
public partial struct IntOrString : ITaggedUnionCases<int, string>
{
}
```

코드 생성은 `TaggedUnion` 어트리뷰트가 적용된 타입만 대상으로 합니다. `ITaggedUnionCases<T1, ...>`만 구현한 타입은 무시되며, `TaggedUnion` 대상은 2개부터 8개까지의 타입 인자를 가진 `ITaggedUnionCases<T1, ...>` 인터페이스를 정확히 하나 직접 구현해야 합니다.

위 코드는 다음과 같은 코드를 생성합니다.(세부 구현은 생략됨)

```csharp
readonly partial struct IntOrString :
    global::System.IEquatable<IntOrString>,
    global::Macaron.Union.ITaggedUnion<int, string>
{
    public static bool operator ==(IntOrString left, IntOrString right);
    public static bool operator !=(IntOrString left, IntOrString right);

    public static implicit operator global::Macaron.Union.TaggedUnion<int, string>(IntOrString value);
    public static implicit operator IntOrString(global::Macaron.Union.TaggedUnion<int, string> value);

    public IntOrString(int value);
    public IntOrString(string value);

    public bool HasValue { get; }
    public object? Value { get; }

    public bool Equals(IntOrString other);

    public override bool Equals(object? obj);
    public override int GetHashCode();
    public override string ToString();

    public bool TryGetValue(out int value);
    public bool TryGetValue([global::System.Diagnostics.CodeAnalysis.NotNullWhen(returnValue: true)] out string? value);

    public void Switch(
        global::System.Action<int> @int,
        global::System.Action<string> @string);

    public void Switch<TState>(
        TState state,
        global::System.Action<int, TState> @int,
        global::System.Action<string, TState> @string);

    public TResult Match<TResult>(
        global::System.Func<int, TResult> @int,
        global::System.Func<string, TResult> @string);

    public TResult Match<TState, TResult>(
        TState state,
        global::System.Func<int, TState, TResult> @int,
        global::System.Func<string, TState, TResult> @string);
}
```

생성된 타입은 비제네릭 `ITaggedUnion`의 `HasValue`와 `Value`, case 목록에 대응하는 제네릭 `ITaggedUnion<T1, ...>`의 `TryGetValue` 계약을 구현합니다.

### 범용 TaggedUnion 사용하기

별도의 이름을 가진 유니온 타입이 필요하지 않다면 arity 2부터 8까지 제공되는 `TaggedUnion<T1, ...>` 구조체를 바로 사용할 수 있습니다.

```csharp
TaggedUnion<int, string> number = 42;
TaggedUnion<int, string> text = "hello";

var numberValue = (int)number;

if (text.TryGetValue(out string? textValue))
{
    Console.WriteLine(textValue);
}
```

모든 타입 매개변수에는 `notnull` 제약이 적용되며 생성자와 암시적 변환에 `null`을 전달하면 `ArgumentNullException`이 발생합니다. `TaggedUnion<T1, ...>`는 항상 `ITaggedUnion<T1, ...>`을 구현하며, 공식 Union을 지원하는 환경이라면 Union 문법을 사용할 수 있습니다.

### `[TaggedUnion]` 유니온에 포함되는 타입의 제약

- 타입은 최소 2개, 최대 8개까지 지정할 수 있습니다.
- 동일한 타입을 지정할 수 없습니다.
- `object`, `Nullable<T>` 타입이 아니어야 하며, 제네릭 타입인 경우 완전히 바인딩되어야 합니다.
- 참조 타입에 nullable 어노테이션을 사용할 수 없습니다.
- 배열이나 제네릭 타입 내부의 nullable 타입은 허용됩니다. 예를 들어 `int?[]`와 `List<string?>`는 사용할 수 있습니다.

### 유니온 인스턴스 생성하기

기본적으로 유니온 타입의 생성자를 사용합니다.

```csharp
var number = new IntOrString(42);
IntOrString text = new("hello");
```

### 캐스팅 연산자 생성하기

`TaggedUnion` 어트리뷰트의 `GenerateConversionOperators`를 `true`로 지정하면 각 타입에서 유니온 타입으로의 암시적 캐스팅과 유니온 타입에서 각 타입으로의 명시적 캐스팅을 생성합니다.

```csharp
[TaggedUnion(GenerateConversionOperators = true)]
public partial struct IntOrString : ITaggedUnionCases<int, string>
{
}

IntOrString number = 42;
IntOrString text = "hello";
var value = (int)number;
```
인터페이스 타입은 C# 언어 제약으로 인해 이 옵션을 활성화해도 캐스팅 연산자를 생성하지 않습니다.

`GenerateConversionOperators` 옵션과 관계없이 생성된 유니온과 같은 케이스 목록의 범용 `TaggedUnion<T1, ...>` 사이에는 양방향 암시적 변환이 항상 생성됩니다. 초기화되지 값은 상대 타입의 `default` 값으로 변환됩니다.

```csharp
var named = new IntOrString(42);
TaggedUnion<int, string> generic = named;
IntOrString roundTripped = generic;
```

### 값을 사용하기

값을 꺼낼 때는 `Value`, `TryGetValue`, 사용할 때는 `Switch`, `Match`를 쓸 수 있습니다.

#### Value 프로퍼티

`Value`는 `object?`를 반환합니다. 차후 도입될 C#의 유니온 문법에 대한 호환성을 위해 존재하는 프로퍼티로 타입 정확성과 전체 타입을 사용했는지에 대한 보장이 없고 값 타입인 경우 박싱이 발생하기 때문에 권장하지 않습니다.

```csharp
var intOrString = new IntOrString(42);

var result = intOrString.Value switch
{
    int i => i,
    string s => 0,
    _ => -1,
};
```

#### TryGetValue 메서드

`TryGetValue`를 사용하여 값 타입도 박싱 없이 값을 꺼낼 수 있습니다.

```csharp
if (intOrString.TryGetValue(out int intVal))
{
    // ...
}
else if (intOrString.TryGetValue(out string? stringVal))
{
    // ...
}
```

#### Switch, Match 메서드

`Switch`, `Match`는 각 타입에 대한 델리게이트를 사용하여 값을 처리할 수 있습니다. 값이 할당되지 않은 유니온 인스턴스에 이 메서드를 호출하면 예외가 발생합니다.

```csharp
// 반환 값이 없는 경우 Switch
intOrString.Switch(
    @int: value => Console.WriteLine($"int: {value}"),
    @string: value => Console.WriteLine($"string: {value}"));

// 반환 값이 있는 경우 Match
var result = number.Match(
    @int: value => $"int: {value}",
    @string: value => $"string: {value}");

// 상태를 함께 전달하는 경우
var lines = new List<string>();

intOrString.Switch(
    lines,
    @int: static (value, state) => state.Add($"int: {value}"),
    @string: static (value, state) => state.Add($"string: {value}"));

var formatted = number.Match(
    "value: ",
    @int: static (value, state) => state + value,
    @string: static (value, state) => state + value);
```

상태를 받는 오버로드는 상태를 첫 번째 매개변수로 받고 각 델리게이트에는 케이스 값, 상태 순서로 전달합니다.

##### Switch, Match의 매개변수 이름

`Switch`와 `Match`의 매개변수 이름은 기본적으로 제네릭을 제외한 타입의 이름을 사용합니다. 다만 타입이 인터페이스인 경우 타입의 이름이 `I`로 시작하고 두 번째 문자가 대문자라면 `I`를 제외한 이름을 사용합니다.

지정된 타입의 이름이 동일한 경우 코드 생성이 실패하며 `TaggedUnionCase` 어트리뷰트를 사용하여 이름을 직접 지정해야 합니다. `TaggedUnionCase`는 이름이 충돌하지 않는 경우에도 사용할 수 있습니다.

```csharp
[TaggedUnion]
[TaggedUnionCase(typeof(Failure), paramName: "error")]
public partial struct Result : ITaggedUnionCases<Success, Failure>
{
}
```

상태 매개변수 이름은 기본적으로 `state`입니다. 확정된 케이스 매개변수 이름 중 `state`가 있다면 `MTU0016` 진단과 함께 코드 생성이 중단됩니다. 이 경우 `TaggedUnion`의 `StateParamName` 프로퍼티에 유효하고 충돌하지 않는 C# 식별자를 지정해야 합니다.

```csharp
[TaggedUnion(StateParamName = "context")]
public partial struct StateOrFailure : ITaggedUnionCases<State, Failure>
{
}
```
