
class UploadManager {

  constructor() {
    this.uploadServer = './'
    this.queue = new ConcurrentUploadQueue(2); // 默认并发数为2
    this.setupEventListeners();
    this.onComplete = null
  }

  // 文件上传函数
  async simulateUpload(file, progressEle) {
    return new Promise((resolve, reject) => {
      let fd = new FormData()
  
      fd.append(fileName, file)
  
      let xhr = new XMLHttpRequest()
  
      xhr.open('POST', this.uploadServer, true)
  
      xhr.onreadystatechange = function () {
        if (xhr.status == 200) {
          progressEle.innerText = `上传完毕`
          resolve(options)
        } else {
          progressEle.innerText = `上传失败`
          reject(options)
        }
      }
  
      // 下载进度
      xhr.onprogress = (e) => {
        // 已上传/总大小 取两位小数
        const p = ((e.loaded / e.total) * 100).toFixed(2)
        progressEle.innerText = `上传中：${p}`
      }
      // 下载进度
  
      xhr.send(fd)
    });
  }

  // 添加文件到上传队列
  addFiles(files) {
    return new Promise(resolve => {
      for (const item of files) {
        this.queue.addTask(
          this.simulateUpload.bind(this),
          item.file,
          item.progressEle
        ).then(result => {
          console.log(`文件 ${item.file.name} 上传成功:`, result);
        }).catch(error => {
          console.error(`文件 ${item.file.name} 上传失败:`, error);
        });
      }
      resolve()
    })
  }

  // 设置事件监听
  setupEventListeners() {
    this.queue.onProgress = (progress) => {
      this.updateProgressUI(progress);
    };

    this.queue.onComplete = (summary) => {
      this.onComplete && this.onComplete(summary)
    };

    this.queue.onError = (error, file) => {
      console.error(`上传错误: ${file.name}`, error);
    };
  }

  // 更新UI进度
  updateProgressUI(progress) {
    const progressBox = document.getElementById('overview');

    if (progressBox) {
      progressBox.innerText =
        `进度: ${progress.completed}/${progress.total} | 运行中: ${progress.running} | 等待中: ${progress.queued}`;
    }
  }

  // 改变并发数
  changeConcurrency(value) {
    this.queue.setConcurrency(value);
    console.log(`并发数已改为: ${value}`);
  }

  // 清除任务
  clear() {
    this.queue.clear()
    const progressBox = document.getElementById('overview')
    if (progressBox) {
      progressBox.innerText = ''
    }
  }
}
