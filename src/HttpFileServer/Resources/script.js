let files = [];
const mainBody = document.querySelector("body");
const uploadManager = new UploadManager();

mainBody.addEventListener("dragover", dragoverHandle);
mainBody.addEventListener("dragleave", dragleaveHandle);
mainBody.addEventListener("drop", dropHandle);

function dragoverHandle(event) {
  event.preventDefault();
}

function dragleaveHandle(event) {
  event.preventDefault();
}

async function dropHandle(event) {
  event.preventDefault();
  const promises = [];

  for (const item of event.dataTransfer.items) {
    const entry = item.webkitGetAsEntry();
    console.log("[ entry ] >", entry);
    promises.push(readFiles(entry));
  }

  const resultFilesArrays = await Promise.all(promises);
  files = resultFilesArrays.flat().reverse();

  let dataStr = files.map(file => `
      <tr class="hover:bg-gray-50 transition-colors">
        <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500">${file.fullPath}</td>
        <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500">${getFileSize(file.size)}</td>
        <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500">${formatDate(file.lastModifiedDate)}</td>
        <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500 uploadStatus">待上传</td>
      </tr>
  `).join('');

  document.querySelector("#fileInfo").innerHTML = dataStr;
  const overview = document.getElementById('overview')
  overview.innerText = `文件总数：${files.length}`

  document.querySelector("#confirm").classList.add("hidden");
  document.querySelector("#cancel").classList.remove("hidden");
  document.querySelector("#uploader").classList.remove("hidden");
  document.querySelector("#uploadModal").classList.remove("hidden");
}
// 读取文件/文件夹
async function readFiles(item) {
  if (item.isDirectory) {
    // 是一个文件夹
    const directoryReader = item.createReader();
    // readEntries是一个异步方法
    const entries = await new Promise((resolve, reject) => {
      directoryReader.readEntries(resolve, reject);
    });

    let files = [];
    for (const entry of entries) {
      const resultFiles = await readFiles(entry);
      files = files.concat(resultFiles);
    }
    return files;
  } else {
    // 是一个文件
    const file = await new Promise((resolve, reject) => {
      item.file(resolve, reject);
    });
    file.fullPath = item.fullPath;
    return [file];
  }
}

async function sendFiles() {
  if (!files.length) return;
  document.querySelector("#confirm").classList.add("hidden");
  document.querySelector("#cancel").classList.remove("hidden");
  document.querySelector("#uploader").classList.add("hidden");
  const statusBoxs = Array.from(document.querySelectorAll(".uploadStatus"));
  const fileItems = statusBoxs.map((e, i) => {
    return {
      file: files[i],
      progressEle: e,
    };
  });

  await uploadManager.addFiles(fileItems);
  uploadManager.queue.getStatus();
  uploadManager.queue.startTask();
  uploadManager.onComplete = (summary) => {
    files = [];
  document.querySelector("#confirm").classList.remove("hidden");
  document.querySelector("#cancel").classList.add("hidden");
  document.querySelector("#uploader").classList.add("hidden");
  };
}

function cancel() {
  files = [];
  document.querySelector("#uploadModal").classList.add("hidden");
  uploadManager.clear();
}

function getFileSize(srcLength) {
  let units = ["B", "KB", "MB", "GB"];
  var v = srcLength;
  var uIndex = 0;
  while (v > 1024 && uIndex < 3) {
    uIndex++;
    v /= 1024;
  }
  return `${Math.round(v * 100) / 100} ${units[uIndex]}`;
}

function formatDate(v) {
  var dt = new Date();
  if (v instanceof Date) dt = v;
  else if (v instanceof Number) {
    dt = new Date(v);
  }
  let num_year = dt.getFullYear();
  let num_month = dt.getMonth() + 1;
  let num_date = dt.getDate();
  let num_hour = dt.getHours();
  let num_min = dt.getMinutes();
  let num_sec = dt.getSeconds();

  return `${num_year}-${num_month < 10 ? "0" + num_month : num_month}-${
    num_date < 10 ? "0" + num_date : num_date
  } ${num_hour < 10 ? "0" + num_hour : num_hour}:${
    num_min < 10 ? "0" + num_min : num_min
  }:${num_sec < 10 ? "0" + num_sec : num_sec}`;
}


function previewFile(event) {
  const fileName = event.currentTarget.getAttribute('data-filename');
  const modal = document.getElementById('previewModal');
  const closeBtn = document.getElementById('closePreview');
  const previewContent = modal.querySelector('.mb-6');

  // 清空之前的内容
  previewContent.innerHTML = '';

  // 根据文件类型动态生成预览内容
  const fileExtension = fileName.split('.').pop().toLowerCase();
  let previewElement;

  if (['png', 'jpg', 'jpeg', 'gif', 'bmp'].includes(fileExtension)) {
    // 图片预览
    previewElement = document.createElement('img');
    previewElement.src = `./${fileName}`;
    previewElement.classList.add('max-w-full', 'h-auto');
  } else if (['txt', 'md', 'html', 'js', 'css'].includes(fileExtension)) {
    // 文本预览
    fetch(`./${fileName}`)
      .then(response => response.text())
      .then(text => {
        previewElement = document.createElement('pre');
        previewElement.textContent = text;
        previewElement.classList.add('whitespace-pre-wrap', 'bg-gray-100', 'p-4', 'rounded-lg', 'max-h-96', 'overflow-auto');
        previewContent.appendChild(previewElement);
      });
    // 直接返回，等待fetch完成
    modal.classList.remove('hidden');
    return;
  } else {
    // 不支持的文件类型
    previewElement = document.createElement('p');
    previewElement.textContent = '不支持预览此文件类型。';
    previewElement.classList.add('text-gray-600');
  }

  previewContent.appendChild(previewElement);
  modal.classList.remove('hidden');

  closeBtn.onclick = function() {
    modal.classList.add('hidden');
  }
}

function closePreview() {
  const modal = document.getElementById('previewModal');
  modal.classList.add('hidden');
}
