一个极速的WebAPI开发库，能够以简单且非侵入的方式将任何.net方法发布为WebAPI。

建设中......

# 入门——三步构建一个WebAPI

假设已有业务逻辑：

    public class SimpleServiceProvider
    {
        private readonly Random _random = new Random();

        public int PlusRandom(int x, int y)
        {
            _lastValue = _random.Next(1000);
            return x + y + _lastValue;
        }
    }

现在让我们按下面的步骤，将上面的PlusRandom方法发布为WebAPI。

1. 新建一个ASP.net项目，并引用此API程序集。
1. 新建一个ashx入口，这里假定其名字为SimpleExample.ashx，让其关联的类代码继承抽象类`SlimApiHttpHandler`。
1. 重写`Setup`方法，代码如下：

        public class SimpleExample : SlimApiHttpHandler
        {
            public override void Setup(ApiSetup setup)
            {
                var serviceProvider = new SimpleServiceProvider();
                setup.Method((Func<int, int, int>)serviceProvider.PlusRandom);
            }
        }

*注：当前支持的.net framwork最低版本为3.5。*

现在WebAPI已经可以使用了，编译运行，并使用下面的链接访问（不同电脑上端口可能不一样）：

    http://localhost:8042/SimpleExample.ashx?~method=PlusRandom&x=13&y=25

得到了返回结果，因为方法中使用随机数，返回结果的`Data`字段可能不尽相同：

    {"Code":0,"Message":"","Data":311}


# 通信协议

上面的简单示例中可以看出，使用了GET方式访问WebAPI，返回结果为JSON。

## 请求的格式

上面的请求链接可以被拆分成三个部分：

- API入口文件：http://localhost:8042/SimpleExample.ashx
- 以 `~` 开头的HTTP参数：~method=PlusRandom
- 其他HTTP参数：x=13，y=25

### 元参数

以 `~` 开头的HTTP参数被用于描述API调用的方式，目前支持的参数有：

- ~method：要调用的WebAPI方法名称。
- ~format：调用方法时传参的格式，支持GET/POST/JSON。
- ~callback：返回JSONP格式的数据，并指定回调函数的名称。

*注：实际上，元参数的名称和参数的值都是大小写不敏感的。*

*注：`~`符号不仅在元参数中使用，还用于其他场景，比如传递简单的数组参数。*

### .net方法的输入参数

元参数以外的其余参数被用于传递.net方法的参数的值，比如上面的例子中的x和y。元参数`~format`指定了传递这些.net方法参数的格式。

#### GET/POST

当`~format`的值为GET或POST时，参数将以HTTP参数的格式传递，比如上例中的x=13&y=25。HTTP参数的值将被传递给具有相同名称的.net方法参数，同时进行必要的类型转换。

*注：目前的实现中，GET和POST是等价的，参数拼在URL里或使用POST方式发送都能够被识别。*

支持HTTP传参的类型有：所有基础类型、String、DateTime、Guid、枚举、由前面这些类型够成的简单集合。对于日期和GUID，传递参数的值需要能被对应类型上的`Parse`方法解析。

使用HTTP参数传递简单的集合时，数组元素间使用 `~` 符号分割，比如 numbers=1~2~3 可将数组 [1, 2, 3] 赋值给方法参数 `IList<int> numbers`。实际上，参数继承自`ICollection<T>`即可，但实现类需要有一个无参数的构造函数。

受限于HTTP参数的表现形式，使用这种方式不能传递复杂的数据结构，在这些场景下，就需要使用JSON格式传参了。

#### JSON

当`~format`的值为JSON时，可以通过POST一个JSON的方式传参，JSON的属性值将被反序列化到与属性同名的.net方法参数。

还是使用上面的例子，改用JSON传参，形式如下：

URL:
 
    http://localhost:8042/SimpleExample.ashx?~method=PlusRandom&~format=json

Post Data:

    { "x":13, "y":25 } 

JSON比起HTTP参数，能够传递更为复杂的数据结构。

#### 只有一个POCO参数的.net方法

在.net方法只有一个POCO参数，且参数类型中的所有属性和字段都能够从HTTP参数转换（参考前面列举的支持HTTP传参的类型），此时，使用HTTP和JSON传参，HTTP参数名称和JSON属性名称将直接和该POCO参数的属性和字段名称进行匹配。

前面的`PlusRandom`方法也可以被改成下面样子，而调用方的请求则不需要改变：

    public class PlusRandomParam
    {
        public int x { get; set; }
        public int y { get; set; }
    }

    public int PlusRandom(PlusRandomParam param)
    {
        // 实现逻辑
    }

这种用法尤其适合一些需要重构的场景，比如一个方法一开始接收三个参数，随着需求的复杂化，参数逐渐增多，就应当将参数重新定义到一个类里去。

## 返回的格式

### JSON

返回值默认使用JSON格式，它包含固定的三个属性：

- Code：返回码，0表示API调用成功，非0为调用异常。目前对于异常状态码还没有明确的定义。
- Message：描述信息，通常用于在调用异常时给出帮助信息。
- Data：API方法的返回值的数据部分，即为API对应的.net方法返回值的JSON序列化结果，可以是常量，也可以是对象、数组。

### JSONP

若请求时指定了~callback参数，则JSON将被放在其所给定的回调函数中，比如对于上面的例子：
    
    http://localhost:8042/SimpleExample.ashx?~method=PlusRandom&~callback=invoke&x=13&y=25

将得到JSONP结果

    invoke({"Code":0,"Message":"","Data":311}) 


# 注册API方法

`SlimApiHttpHandler.Setup`方法提供了API注册的入口。

## 注册API方法

### 通过委托注册

### 通过`MethodInfo`注册

### 自动批量注册

## 开启缓存