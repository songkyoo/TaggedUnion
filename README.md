# TaggedUnion

`TaggedUnion`은 C#에서 구별된 유니온(discriminated union)을 만들어주는 소스 제네레이터입니다.

## 패키지 만들기

다음 명령으로 NuGet 패키지를 생성할 수 있습니다.

```shell
dotnet pack ./TaggedUnion/TaggedUnion.csproj -c Release
```

`./TaggedUnion/bin/Release/` 폴더에 `nupkg` 확장자를 가지는 패키지가 생성됩니다.

## 사용법

유니온으로 사용할 타입을 `readonly partial struct`로 선언하고 `TaggedUnion` 어트리뷰트를 사용하여 포함될 타입을 지정합니다. `TaggedUnion`이 선언되는 타입은 제네릭이 아니어야 하고 인스턴스 생성자를 가질 수 없습니다.

```csharp
using Macaron.Union;

[TaggedUnion(typeof(int), typeof(string))]
public readonly partial struct IntOrString
{
}
```

위 코드는 다음과 같은 코드를 생성합니다.(세부 구현은 생략됨)

```csharp
partial struct IntOrString : global::System.IEquatable<IntOrString>
{
    public static bool operator ==(IntOrString left, IntOrString right);
    public static bool operator !=(IntOrString left, IntOrString right);

    public static implicit operator IntOrString(int value);
    public static explicit operator int(IntOrString value);

    public static implicit operator IntOrString(string value);
    public static explicit operator string(IntOrString value);

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
        global::System.Action<string> @string
    );

    public TResult Match<TResult>(
        global::System.Func<int, TResult> @int,
        global::System.Func<string, TResult> @string
    );
}
```

### 유니온에 포함되는 타입의 제약

- 타입은 최소 2개, 최대 8개까지 지정할 수 있습니다.
- 동일한 타입을 지정할 수 없습니다.
- `void`, `object`, `Nullable<T>`가 아니어야 하며, 제네릭 타입인 경우 완전히 바인딩되어야 합니다.

### 유니온 인스턴스 생성하기

유니온에 포함된 타입은 암시적으로 유니온 타입으로 변환됩니다.

```csharp
IntOrString number = 42;
IntOrString text = "hello";
```

### 값을 사용하기

값을 꺼낼 때는 `Value`, `TryGetValue`, 사용할 때는 `Switch`, `Match`를 쓸 수 있습니다.

`Value`는 `object?`를 반환합니다. 차후 도입될 C#의 유니온 문법에 대한 호환성을 위해 존재하는 프로퍼티로 타입 정확성과 전체 타입을 사용했는지에 대한 보장이 없고 값 타입인 경우 박싱이 발생하기 때문에 권장하지 않습니다.

```csharp
var intOrString = (IntOrString)42;

var result = intOrString.Value switch
{
    int i => i,
    string s => 0,
    _ => -1,
};
```

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

`Switch`, `Match`는 각 타입에 대한 델리게이트를 사용하여 값을 처리할 수 있습니다. 값이 할당되지 않은 유니온 인스턴스에 이 메서드를 호출하면 예외가 발생합니다.

```csharp
// 반환 값이 없는 경우 Switch
intOrString.Switch(
    @int: value => Console.WriteLine($"int: {value}"),
    @string: value => Console.WriteLine($"string: {value}")
);

// 반환 값이 있는 경우 Match
var result = number.Match(
    @int: value => $"int: {value}",
    @string: value => $"string: {value}"
);
```

## Switch, Match의 매개변수 이름

`Switch`와 `Match`의 매개변수 이름은 기본적으로 제네릭을 제외한 타입의 이름을 사용합니다. 다만 타입이 인터페이스인 경우 타입의 이름이 `I`로 시작하고 두 번째 문자가 대문자라면 `I`를 제외한 이름을 사용합니다.

지정된 타입의 이름이 동일한 경우 코드 생성이 실패하며 `TaggedUnionCase` 어트리뷰트를 사용하여 이름을 직접 지정해야 합니다. `TaggedUnionCase`는 이름이 충돌하지 않는 경우에도 사용할 수 있습니다.

```csharp
[TaggedUnion(typeof(Success), typeof(Failure))]
[TaggedUnionCase(typeof(Failure), paramName: "error")]
public readonly partial struct Result
{
}
```
