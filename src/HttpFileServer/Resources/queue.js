class ConcurrentUploadQueue {
  constructor(maxConcurrent = 3) {
    this.maxConcurrent = maxConcurrent;
    this.running = 0;
    this.queue = [];
    this.completed = 0;
    this.total = 0;
    this.results = [];
    this.errors = [];

    // 事件回调函数
    this.onProgress = null;
    this.onComplete = null;
    this.onError = null;
  }

  // 添加上传任务
  addTask(uploadFunction, file, options = {}) {
    return new Promise((resolve, reject) => {
      this.queue.push({
        uploadFunction,
        file,
        options,
        resolve,
        reject
      });
      this.total++;
    });
  }

  // 开始执行任务队列
  startTask() {
    this._next();
  }

  // 执行下一个任务
  _next() {
    if (this.running >= this.maxConcurrent || this.queue.length === 0) {
      return;
    }

    const task = this.queue.shift();
    this.running++;

    // 更新进度
    this._updateProgress();

    task.uploadFunction(task.file, task.options)
      .then(result => {
        this.results.push({
          file: task.file,
          result,
          timestamp: new Date()
        });
        task.resolve(result);
      })
      .catch(error => {
        this.errors.push({
          file: task.file,
          error,
          timestamp: new Date()
        });
        task.reject(error);

        // 触发错误回调
        if (this.onError) {
          this.onError(error, task.file);
        }
      })
      .finally(() => {
        this.running--;
        this.completed++;
        this._updateProgress();

        // 检查是否全部完成
        if (this.completed === this.total && this.running === 0) {
          this._onComplete();
        }

        this._next();
      });

    if (this.running < this.maxConcurrent || this.queue.length === 0) {
      this._next();
    }
  }

  // 更新进度
  _updateProgress() {
    if (!this.running && !this.queue.length && !this.completed && !this.total) return
    if (this.onProgress) {
      this.onProgress({
        completed: this.completed,
        total: this.total,
        running: this.running,
        queued: this.queue.length,
        progress: this.total > 0 ? (this.completed / this.total) * 100 : 0
      });
    }
  }

  // 全部完成回调
  _onComplete() {
    if (this.onComplete) {
      this.onComplete({
        results: this.results,
        errors: this.errors,
        total: this.total,
        completed: this.completed
      });
    }
  }

  // 设置并发数
  setConcurrency(maxConcurrent) {
    this.maxConcurrent = maxConcurrent;

    // 如果并发数增加，立即启动更多任务
    while (this.running < this.maxConcurrent && this.queue.length > 0) {
      this._next();
    }
  }

  // 获取队列状态
  getStatus() {
    return {
      running: this.running,
      queued: this.queue.length,
      completed: this.completed,
      total: this.total,
      progress: this.total > 0 ? (this.completed / this.total) * 100 : 0,
      results: this.results,
      errors: this.errors
    };
  }

  // 清理队列
  clear() {
    this.running = 0;
    this.queue = [];
    this.completed = 0;
    this.total = 0;
    this.results = [];
    this.errors = [];
  }
}
