
using cloud.core;
using cloud.core.mongodb;
using projectapi.Controllers;

namespace projectapi.Models;

public class SampleMongodbConnect : BaseMongoObjectIdDbContext
{
    public SampleMongodbConnect() : base(AppSettingsHelper.GetValueByKey("SampleMongodbConnect:ConnectionString")){}

    public required DbSetObjectId<ClassEntity> NamClass { get; set;}  

    public required DbSetObjectId<StudentEntity> NamStudent { get; set;}     

}
